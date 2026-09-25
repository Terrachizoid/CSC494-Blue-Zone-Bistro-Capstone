$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Test-GameOperations.ps1"
$csvRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets/GameData/CSV'
function Read-ContentCsv($name) { @(Import-Csv (Join-Path $csvRoot "Blue_Zone_Bistro_$name.csv")) }
$foodsById = @{}
foreach ($row in (Read-ContentCsv 'Foods')) {
    if ($foodsById.ContainsKey($row.id)) { throw 'Duplicate food ID' }
    $food = New-Object FoodData
    $food.id = $row.id
    $food.recipeCategory = [FoodCategory]$row.recipe_category
    $food.trafficLight = [TrafficLight]$row.traffic_light
    $food.storageType = [StorageType]$row.storage_type
    $food.dailyDozenCategories = @($row.daily_dozen_categories.Split('|') | ForEach-Object { [DailyDozenCategory]$_ })
    if ($food.trafficLight -eq [TrafficLight]::Unassigned -or [int]$row.ingredient_count -le 0) { throw 'Unconfigured food' }
    $foodsById[$row.id] = $food
}
$foodDb = New-Object FoodDatabase
$foodDb.SetItems([System.Collections.Generic.List[FoodData]]@($foodsById.Values))
$storesById = @{}
foreach ($row in (Read-ContentCsv 'Stores')) {
    $store = New-Object StoreData
    $store.id = $row.store_id; $store.unlockDay = [int]$row.unlock_day
    $store.requiresMembership = [bool]::Parse($row.requires_membership)
    $storesById[$store.id] = $store
}
$listingItems = New-Object 'System.Collections.Generic.List[StoreListingData]'
foreach ($row in (Read-ContentCsv 'Prices')) {
    $listing = New-Object StoreListingData
    $listing.id=$row.listing_id; $listing.food=$foodsById[$row.food_id]; $listing.store=$storesById[$row.store_id]
    $listing.gamePrice=[float]$row.game_price; $listing.servingsPerPackage=[int]$row.servings_per_package
    $listing.unlockDay=[int]$row.unlock_day; $listing.requiredFlag=$row.required_flag
    if (!$listing.food -or !$listing.store -or $listing.gamePrice -le 0 -or $listing.servingsPerPackage -le 0) { throw 'Invalid listing' }
    $listingItems.Add($listing)
}
$listingDb = New-Object StoreListingDatabase; $listingDb.SetItems($listingItems)
$foodDb.RebuildMinimumPrices($listingItems)
$recipeItems = New-Object 'System.Collections.Generic.List[RecipeData]'
foreach ($row in (Read-ContentCsv 'Recipes')) {
    $recipe = New-Object RecipeData
    $recipe.id=$row.recipe_id; $recipe.unlockDay=[int]$row.unlock_day
    $recipe.displayName=$row.display_name; $recipe.tier=[RecipeTier]$row.tier
    $recipe.baseSalePrice=[float]$row.base_sale_price; $recipe.delightValue=[int]$row.delight_value
    $recipe.allowNonGreenChoices=[bool]::Parse($row.allow_non_green_choices)
    $slots = New-Object 'System.Collections.Generic.List[RecipeSlot]'
    foreach ($column in @('required_slots','optional_slots')) {
        foreach ($value in $row.$column.Split('|')) {
            if (!$value) { continue }
            $pair=$value.Split(':'); $slot=New-Object RecipeSlot
            $slot.category=[FoodCategory]$pair[0]; $slot.servings=[int]$pair[1]; $slot.optional=$column -eq 'optional_slots'
            if ($slot.servings -le 0) { throw 'Invalid recipe slot' }; $slots.Add($slot)
        }
    }
    $recipe.slots=$slots.ToArray(); $recipeItems.Add($recipe)
}
$recipeDb=New-Object RecipeDatabase; $recipeDb.SetItems($recipeItems)
$customerItems=New-Object 'System.Collections.Generic.List[CustomerData]'
foreach ($row in (Read-ContentCsv 'Customers')) {
    $customer=New-Object CustomerData; $customer.id=$row.customer_id
    $customer.startingHealth=[int]$row.starting_health; $customer.unlockDay=[int]$row.unlock_day
    $customer.customerType=[CustomerType]$row.customer_type; $customer.displayName=$row.display_name
    foreach ($recipeId in $row.preferred_recipe_ids.Split('|')) { if ($recipeId -and !$recipeDb.GetById($recipeId)) { throw 'Unknown preferred recipe' } }
    $customerItems.Add($customer)
}
$customerDb=New-Object CustomerDatabase; $customerDb.SetItems($customerItems)
# Every template must have a valid green build from listings available on its unlock day.
# This catches new recipes that look valid structurally but cannot actually be stocked.
foreach ($recipe in $recipeItems) {
    $recipeState=New-Object GameSessionState
    $recipeState.currentDay=$recipe.unlockDay; $recipeState.money=1000
    $recipeInput=New-Object ServeInput; $recipeInput.customerId='U001'; $recipeInput.recipeId=$recipe.id
    $recipeBalance=New-Object BalanceConfig
    $recipeBuyer=[PurchaseService]::new($recipeState,$listingDb,$recipeBalance)
    foreach ($slot in $recipe.slots | Where-Object { !$_.optional }) {
        $offer=$listingItems | Where-Object {
            $_.food.recipeCategory -eq $slot.category -and $_.food.trafficLight -eq [TrafficLight]::Green -and
            $_.unlockDay -le $recipe.unlockDay -and $_.store.unlockDay -le $recipe.unlockDay -and
            !$_.store.requiresMembership -and !$_.requiredFlag
        } | Sort-Object { $_.gamePrice / $_.servingsPerPackage } | Select-Object -First 1
        if (!$offer) { throw "Recipe $($recipe.id) has no accessible green ingredient for $($slot.category)" }
        $remaining=$slot.servings
        while ($remaining -gt 0) {
            $purchase=$recipeBuyer.Buy($offer.id)
            if (!$purchase.success) { throw $purchase.error }
            $ingredient=New-Object IngredientSelection
            $ingredient.inventoryEntryId=$purchase.inventoryEntryId
            $ingredient.servingsUsed=[Math]::Min($remaining,$purchase.servingsAdded)
            $remaining-=$ingredient.servingsUsed; $recipeInput.ingredients.Add($ingredient)
        }
    }
    $recipeMeal=[MealService]::new($recipeState,$foodDb,$recipeDb,$customerDb,$recipeBalance)
    $evaluation=$recipeMeal.Preview($recipeInput)
    if (!$evaluation.success -or !$evaluation.recipeBuilt -or !([RecipeCheck]::new($foodDb)).CanMake($recipe,$recipeState)) {
        throw "Recipe $($recipe.id) failed availability/preview agreement: $($evaluation.error)"
    }
    $servedRecipe=$recipeMeal.Serve($recipeInput)
    if (!$servedRecipe.success -or !$servedRecipe.recipeBuilt) { throw "Recipe $($recipe.id) could not be served" }
}
Write-Output "PASS: all $($recipeItems.Count) recipes can be purchased and built on their unlock day."
$dialogueItems=New-Object 'System.Collections.Generic.List[DialogueSequenceData]'
$dialogueById=@{}
foreach ($row in (Read-ContentCsv 'DialogueSequences')) {
    if ($dialogueById.ContainsKey($row.sequence_id)) { throw 'Duplicate dialogue ID' }
    $sequence=New-Object DialogueSequenceData; $sequence.id=$row.sequence_id
    $sequence.mode=[DialogueMode]$row.mode; $sequence.trigger=[DialogueTrigger]$row.trigger
    $sequence.repeatRule=[DialogueRepeatRule]$row.repeat_rule; $sequence.priority=[int]$row.priority
    $sequence.customer=$customerDb.GetById($row.customer_id)
    if ($row.customer_id -and !$sequence.customer) { throw 'Missing dialogue customer' }
    $sequence.recipeBuildCondition=[RecipeBuildCondition]$row.recipe_build_condition
    if ($row.meal_health_condition) { $sequence.mealHealthCondition=[MealHealthCondition]$row.meal_health_condition }
    foreach ($mapping in @(@('min_day','minDay'),@('max_day','maxDay'),@('min_customer_health','minCustomerHealth'),
        @('max_customer_health','maxCustomerHealth'),@('min_customer_delight','minCustomerDelight'),
        @('max_customer_delight','maxCustomerDelight'),@('min_served_meal_delight','minServedMealDelight'),
        @('max_served_meal_delight','maxServedMealDelight'))) {
        $raw=$row.($mapping[0]); if ($raw -ne '') { $sequence.($mapping[1])=[int]$raw }
    }
    foreach ($mapping in @(@('required_flags','requiredFlags'),@('forbidden_flags','forbiddenFlags'),
        @('set_flags','setFlags'),@('clear_flags','clearFlags'))) {
        $sequence.($mapping[1])=@($row.($mapping[0]).Split('|') | Where-Object { $_ })
    }
    $sequence.requiredRecipeTiers=@($row.required_recipe_tiers.Split('|') | Where-Object { $_ } | ForEach-Object { [RecipeTier]$_ })
    $dialogueById[$sequence.id]=$sequence; $dialogueItems.Add($sequence)
}
$lineKeys=@{}
foreach ($row in (Read-ContentCsv 'DialogueLines')) {
    $sequence=$dialogueById[$row.sequence_id]
    $key=$row.sequence_id+':'+$row.line_order
    if (!$sequence -or $lineKeys.ContainsKey($key) -or !$row.text_template) { throw 'Invalid dialogue line/reference' }
    $lineKeys[$key]=$true
    $line=New-Object DialogueLineData; $line.lineOrder=[int]$row.line_order; $line.speakerId=$row.speaker_id
    $line.textTemplate=$row.text_template; $sequence.lines += $line
}
foreach ($sequence in $dialogueItems) { if (!$sequence.lines.Count) { throw 'Empty dialogue sequence' } }
$dialogueDb=New-Object DialogueDatabase; $dialogueDb.SetItems($dialogueItems)
$state=New-Object GameSessionState; $balance=New-Object BalanceConfig
$buy=[PurchaseService]::new($state,$listingDb,$balance)
$meals=[MealService]::new($state,$foodDb,$recipeDb,$customerDb,$balance)
$days=[DayService]::new($state,$balance,$customerDb)
# Seeded RNG makes this CSV integration simulation repeatable while retaining probabilistic attendance.
$visitRandom = [System.Random]::new(42)
$visits=[CustomerVisitService]::new($state,$customerDb,$balance,[System.Func[double]]{ $visitRandom.NextDouble() })
$scheduler=[EncounterScheduler]::new($state,$dialogueDb,$customerDb,$recipeDb)
$selector=$scheduler.Dialogue
foreach ($customer in $customerItems) {
    foreach ($health in @(0,34,35,69,70,100)) {
        foreach ($delight in @(0,34,35,69,70,100)) {
            $context=New-Object DialogueRuntimeContext
            $context.currentDay=[Math]::Max(2,$customer.unlockDay); $context.customerId=$customer.id; $context.customerHealth=$health; $context.customerDelight=$delight
            $greeting=$selector.FindNext([DialogueTrigger]::CustomerArrival,$context)
            $matches=@($dialogueItems | Where-Object { $_.mode -eq [DialogueMode]::Mandatory -and $selector.IsEligible($_,[DialogueTrigger]::CustomerArrival,$context) })
            if (!$greeting -or $matches.Count -ne 1) { throw "Greeting bands overlap or leave a gap: $($customer.id) health=$health delight=$delight" }
        }
    }
    $context=New-Object DialogueRuntimeContext
    $context.currentDay=[Math]::Max(2,$customer.unlockDay); $context.customerId=$customer.id; $context.customerHealth=55; $context.customerDelight=53
    $context.hasServedMeal=$true; $context.servedRecipeBuilt=$true; $context.servedMealHealthDelta=5; $context.servedMealDelight=3
    $context.servedRecipe=$recipeDb.GetById('R001')
    $reaction=$selector.FindNext([DialogueTrigger]::AfterService,$context)
    if (!$reaction -or !$reaction.id.EndsWith('_AFTER_OKAY')) { throw 'Successful +5 health/+3 delight selected wrong reaction' }
    $context.servedRecipeBuilt=$false; $context.servedMealDelight=-30; $context.servedMealHealthDelta=20
    if (!$selector.FindNext([DialogueTrigger]::AfterService,$context).id.EndsWith('_AFTER_POOR')) { throw 'Failed recipe got positive reaction' }
    $context.servedMealDelight=0
    if (!$selector.FindNext([DialogueTrigger]::AfterService,$context).id.EndsWith('_AFTER_POOR')) { throw 'Delight floor masked failed recipe' }
    $context.servedRecipeBuilt=$true; $context.servedMealHealthDelta=-5; $context.servedMealDelight=3
    if (!$selector.FindNext([DialogueTrigger]::AfterService,$context).id.EndsWith('_AFTER_HEALTH_LOSS')) { throw 'Negative meal health got positive reaction' }
}
Write-Output "PASS: $($customerItems.Count * 36) greeting boundary combinations, successful meals, failed green meals, delight-floor failures, and negative-health reactions."
$loop=[GameLoop]::new($buy,$meals,$days,$visits,$scheduler)
for ($day=1; $day -le 6; $day++) {
    # Stock for every unlocked customer so the test also covers a worst-case attendance roll.
    $requiredBowls=@($customerItems | Where-Object { $_.unlockDay -le $day -and $_.customerType -ne [CustomerType]::VisitingChef }).Count
    foreach ($foodId in @('P001','P002','P008')) {
        $stock=($state.inventory | Where-Object foodId -eq $foodId | Measure-Object servings -Sum).Sum
        $listing=$listingItems | Where-Object { $_.food.id -eq $foodId -and $_.store.id -eq 'S002' } | Select-Object -First 1
        while ($stock -lt $requiredBowls) {
            $result=$loop.Buy($listing.id)
            if (!$result.success) { throw "Day $day purchase failed: $($result.error)" }
            $stock += $result.servingsAdded
        }
    }
    if (!([RecipeCheck]::new($foodDb)).CanMake($recipeDb.GetById('R002'),$state)) { throw 'Daily bowl not makeable' }
    $loop.BeginService() | Out-Null
    if ($day -eq 3 -and $loop.Dialogue.Sequence.id -ne 'D_CHEF_OKINAWA_ARRIVAL') { throw 'CSV chef not first on day three' }
    $dialogueSteps=0
    while ($loop.Phase -eq [GamePhase]::Service) {
        if ($loop.Dialogue.IsActive) {
            $dialogueSteps++
            if ($dialogueSteps -gt 200) { throw 'Dialogue loop did not terminate' }
            if ($loop.Dialogue.CurrentText -match '\{[A-Za-z_]+\}') { throw 'Unresolved dialogue token' }
            $loop.AdvanceDialogue() | Out-Null
            continue
        }
        $inputMeal=New-Object ServeInput; $inputMeal.customerId=$loop.Service.CurrentCustomerId; $inputMeal.recipeId='R002'
        foreach ($foodId in @('P001','P002','P008')) {
            $entry=$state.inventory | Where-Object { $_.foodId -eq $foodId -and $_.servings -gt 0 } | Select-Object -First 1
            $selection=New-Object IngredientSelection; $selection.inventoryEntryId=$entry.inventoryEntryId; $selection.servingsUsed=1
            $inputMeal.ingredients.Add($selection)
        }
        $result=$loop.Serve($inputMeal)
        if (!$result.success) { throw $result.error }
        $loop.ContinueAfterDish() | Out-Null
    }
    Write-Output "CSV simulation day ${day}: spent $($loop.Summary.moneySpent), earned $($loop.Summary.moneyEarned), money $($state.money), dialogue lines $dialogueSteps"
    $loop.FinishDay() | Out-Null
}
if ($loop.Phase -ne [GamePhase]::Completed) { throw 'Did not complete six days' }
if (!$state.flags.HasFlag('okinawa_chef_arrived')) { throw 'Chef CSV did not set shared flag' }
Write-Output 'PASS: CSV enums/references, positive purchase values, and six-day service simulation using CSV content.'

// Real Unity JsonUtility/PlayerPrefs smoke test. Uses a unique key, never the player's checkpoint.
using System;
using UnityEditor;
using UnityEngine;
public static class CheckpointSmokeTest
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Blue Zone Bistro/Test Checkpoint Round Trip")]
    public static void Run()
    {
        string key="Bistro.test."+Guid.NewGuid().ToString("N");
        try
        {
            var store=new CheckpointStore(key);
            var state=new GameSessionState{currentDay=2,money=19.5f};
            var balance=ScriptableObject.CreateInstance<BalanceConfig>();
            CommunityProgress.Initialize(state,balance);
            state.popularity=0;state.communityWellbeing=62.5f;
            state.attendancePlanDay=2;state.dailyCustomerCapacity=1;state.dailyCustomerIds.Add("U");state.unlockedCustomerIds.Add("U");
            state.flags.SetFlag("test.flag");state.correctlySortedFoodIds.Add("F");state.storeMembershipIds.Add("S");
            state.tutorials.Add(new TutorialRecord{tutorialId="T_SHOPPING",status="completed",stepId="shop"});
            state.tutorials.Add(new TutorialRecord{tutorialId="T_COOKING",status="skipped",skipReason="Unavailable ingredients"});
            state.inventory.Add(new InventoryEntry{foodId="F",listingId="L",servings=3,daysRemaining=1,purchaseCostPerServing=.75f});
            var customer=new CustomerState{customerId="U",crashDialoguePending=true,visualScore=-15,rushDay=1};
            customer.cravingDay=2;customer.activeCravings=new[]{CravingTag.Hearty};
            customer.healthHistory.Add(new HealthSnapshot{day=1,health=55,healthDelta=5});state.customers.Add(customer);
            var sequence=ScriptableObject.CreateInstance<DialogueSequenceData>();sequence.id="D";sequence.repeatRule=DialogueRepeatRule.Once;
            state.dialogueHistory.MarkPlayed(sequence,1);
            string snapshot=store.Capture(state);
            Check(store.Save(snapshot,out var error),error);
            state.money=0;state.inventory.Clear();
            Check(new CheckpointStore(key).TryLoad(out var loaded,out error),error);
            CommunityProgress.Initialize(loaded,balance);
            Check(loaded.popularity==0&&loaded.communityWellbeing==62.5f&&loaded.attendancePlanDay==2&&
                loaded.dailyCustomerIds[0]=="U"&&loaded.unlockedCustomerIds[0]=="U"&&loaded.customers[0].activeCravings[0]==CravingTag.Hearty,
                "Community state or morning plan lost");
            var legacy=new GameSessionState();
            Check(CheckpointStore.Decode(store.Capture(legacy),out var migrated,out error),error);
            CommunityProgress.Initialize(migrated,balance);
            Check(migrated.popularity==balance.startingPopularity&&migrated.communityStateVersion==1,"Legacy migration failed");
            Check(loaded.money==19.5f&&loaded.currentDay==2&&loaded.flags.HasFlag("test.flag")&&loaded.inventory.Count==1&&
                loaded.inventory[0].purchaseCostPerServing==.75f&&loaded.customers[0].healthHistory[0].healthDelta==5&&
                loaded.customers[0].crashDialoguePending&&loaded.customers[0].visualScore==-15&&loaded.correctlySortedFoodIds.Count==1&&
                loaded.storeMembershipIds.Count==1&&!loaded.dialogueHistory.CanPlay(sequence,2)&&loaded.tutorials.Count==2&&
                loaded.tutorials[0].status=="completed"&&loaded.tutorials[1].skipReason=="Unavailable ingredients","Session round trip lost data");
            loaded.currentDay=3;Check(store.Save(store.Capture(loaded),out error),error);
            PlayerPrefs.SetString(key,"damaged");PlayerPrefs.Save();
            Check(store.TryLoad(out loaded,out error)&&loaded.currentDay==2,"Previous checkpoint recovery failed");
            Check(!CheckpointStore.Decode(snapshot.Replace("19.5","18.5"),out _,out _),"Checksum failed");
            loaded.gameCompleted=true;loaded.completedDaySummary=new DaySummary{moneyEarned=12};
            Check(CheckpointStore.Decode(store.Capture(loaded),out var completed,out error)&&completed.gameCompleted&&completed.completedDaySummary.moneyEarned==12,"Completion lost");
            UnityEngine.Object.DestroyImmediate(sequence);
            UnityEngine.Object.DestroyImmediate(balance);
            Debug.Log("PASS: Unity checkpoint round trip, private flags/history, detached snapshot, recovery, checksum, completion.");
        }
        finally{PlayerPrefs.DeleteKey(key);PlayerPrefs.DeleteKey(key+".backup");PlayerPrefs.Save();}
    }
}

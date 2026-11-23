// Presentation-only rush/crash state. Never writes health, health history, delight or finance.
public static class RushCrashProgress
{
    public static void Served(CustomerState customer,int day,bool red,int boost=20)
    {
        if(!red)return;
        customer.rushDay=day;
        customer.visualScore=boost;
    }
    public static void StartDay(CustomerState customer,int day,int drop=20)
    {
        customer.visualScore=0;
        if(customer.rushDay>0 && customer.rushDay==day-1)
        {
            customer.visualScore=-drop;
            customer.crashDialoguePending=true;
        }
    }
    public static void CompleteCrash(CustomerState customer,GameFlagState flags)
    {
        customer.crashDialoguePending=false;
        flags.SetFlag(TutorialFlags.CrashExplained);
    }
}

// Local versioned checkpoints, stored through Unity's browser/desktop PlayerPrefs backend.
// The previous checkpoint is retained as a recovery copy. Never serialize Unity object references.
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
public class CheckpointStore
{
    private readonly string Key;
    public CheckpointStore(string storageKey="BlueZoneBistro.checkpoint.v1") { Key=storageKey; }
    [Serializable] private class Envelope { public int version; public string payload; public string checksum; }
    public bool Exists => PlayerPrefs.HasKey(Key) || PlayerPrefs.HasKey(Key+".backup");
    private static string Digest(string text)
    {
        using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
    public string Capture(GameSessionState state)
    {
        string payload=JsonUtility.ToJson(state);
        return JsonUtility.ToJson(new Envelope{version=1,payload=payload,checksum=Digest(payload)});
    }
    public static bool Decode(string json,out GameSessionState state,out string error)
    {
        state=null;error=null;
        try
        {
            var envelope=JsonUtility.FromJson<Envelope>(json);
            if(envelope==null || envelope.version!=1)throw new Exception("Unsupported save version.");
            if(string.IsNullOrEmpty(envelope.payload)||Digest(envelope.payload)!=envelope.checksum)throw new Exception("Checkpoint is incomplete or damaged.");
            var candidate=JsonUtility.FromJson<GameSessionState>(envelope.payload);
            if(candidate==null || candidate.currentDay<1 || float.IsNaN(candidate.money) || float.IsInfinity(candidate.money) || candidate.money<0 ||
                candidate.inventory==null || candidate.customers==null || candidate.flags==null || candidate.dialogueHistory==null ||
                candidate.storeMembershipIds==null || candidate.correctlySortedFoodIds==null)throw new Exception("Checkpoint has invalid session data.");
            foreach(var batch in candidate.inventory)
                if(batch==null || string.IsNullOrEmpty(batch.inventoryEntryId) || string.IsNullOrEmpty(batch.foodId) || batch.servings<=0 || batch.daysRemaining < -1)
                    throw new Exception("Checkpoint contains invalid inventory.");
            foreach(var customer in candidate.customers)
                if(customer==null || string.IsNullOrEmpty(customer.customerId) || customer.healthHistory==null)throw new Exception("Checkpoint contains invalid customer data.");
            if(candidate.communityStateVersion<0||candidate.communityStateVersion>1)throw new Exception("Unsupported community state version.");
            if(candidate.communityStateVersion==1)
            {
                if(!CommunityProgress.Finite(candidate.popularity)||candidate.popularity<0||
                    !CommunityProgress.Finite(candidate.communityWellbeing)||candidate.communityWellbeing<0||
                    !CommunityProgress.Finite(candidate.morningPopularity)||candidate.attendancePlanDay<0||candidate.attendancePlanDay>candidate.currentDay||
                    candidate.dailyCustomerIds==null||candidate.unlockedCustomerIds==null)
                    throw new Exception("Checkpoint contains invalid bistro progress.");
                var ids=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(string id in candidate.dailyCustomerIds)
                    if(string.IsNullOrWhiteSpace(id)||!ids.Add(id)||!candidate.customers.Exists(c=>string.Equals(c.customerId,id,StringComparison.OrdinalIgnoreCase)))
                        throw new Exception("Checkpoint contains an invalid attendance plan.");
                foreach(var c in candidate.customers)
                {
                    if(c.activeCravings==null)c.activeCravings=Array.Empty<CravingTag>();
                    foreach(var craving in c.activeCravings)if(!Enum.IsDefined(typeof(CravingTag),craving)||craving==CravingTag.None)
                        throw new Exception("Checkpoint contains an invalid craving.");
                }
            }
            // There is deliberately no in-progress transaction or UI selection on resume.
            candidate.shoppingReceipt=null;candidate.currentCustomerId=null;candidate.selectedRecipeId=null;
            state=candidate;return true;
        }
        catch(Exception ex){error=ex.Message;return false;}
    }
    public bool TryLoad(out GameSessionState state,out string message)
    {
        state=null;message="No checkpoint yet.";
        if(PlayerPrefs.HasKey(Key) && Decode(PlayerPrefs.GetString(Key),out state,out message))return true;
        if(PlayerPrefs.HasKey(Key+".backup") && Decode(PlayerPrefs.GetString(Key+".backup"),out state,out var backupError))
        {message="Recovered the previous checkpoint because the latest save could not be read.";return true;}
        return false;
    }
    public bool Save(string snapshot,out string error)
    {
        if(!Decode(snapshot,out _,out error))return false;
        string previous=PlayerPrefs.GetString(Key,"");
        try
        {
            if(Decode(previous,out _,out _))PlayerPrefs.SetString(Key+".backup",previous);
            PlayerPrefs.SetString(Key,snapshot);PlayerPrefs.Save();return true;
        }
        catch(Exception ex)
        {
            if(string.IsNullOrEmpty(previous))PlayerPrefs.DeleteKey(Key);else PlayerPrefs.SetString(Key,previous);
            error="Could not save progress: "+ex.Message;return false;
        }
    }
}

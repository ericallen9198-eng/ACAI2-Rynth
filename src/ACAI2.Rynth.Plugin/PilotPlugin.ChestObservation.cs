using ACAI2.Rynth.Core.Keyfer;
namespace ACAI2.Rynth.Plugin;

public sealed partial class PilotPlugin
{
    private uint signaledChest;
    private DateTime nextChestPoll;
    private readonly Dictionary<uint,(string Chest,DateTime Seen)> chestItems=[];
    private readonly Dictionary<uint,DateTime> receiptIds=[];
    public override void OnViewObjectContents(uint id)=>Volatile.Write(ref signaledChest,id);
    private void SampleOwnChest(KeyferSession session,uint player,DateTime now)
    {
        uint signaled=Interlocked.Exchange(ref signaledChest,0);
        if(now<nextChestPoll&&signaled==0)return;nextChestPoll=now.AddMilliseconds(200);
        if(Host.HasGetPluginExportJson)
            session.ObserveChest(Host.GetPluginExportJson("RynthAi","RynthPluginGetItemToolJson"));
        if(!Host.HasGetGroundContainerId||!Host.HasGetContainerContents||!Host.HasGetObjectOwnershipInfo)return;
        uint container=signaled!=0?signaled:Host.GetGroundContainerId();
        if(container!=0&&Host.TryGetObjectName(container,out string chest)&&chest.Length<=120&&chest.EndsWith("Chest",StringComparison.OrdinalIgnoreCase))
        {
            var ids=new uint[512];int count=Host.GetContainerContents(container,ids);
            if(count>=0&&count<ids.Length)for(int i=0;i<count&&chestItems.Count<512;i++)
            {
                uint id=ids[i];
                if(!receiptIds.ContainsKey(id)&&Host.TryGetObjectOwnershipInfo(id,out uint owner,out _,out _)&&owner==container)
                    chestItems[id]=(chest,now);
            }
        }
        foreach(var pair in chestItems.ToArray())
        {
            uint id=pair.Key;
            if(now-pair.Value.Seen>TimeSpan.FromSeconds(15)){chestItems.Remove(id);continue;}
            if(!Host.TryGetObjectOwnershipInfo(id,out uint owner,out uint wearer,out _))continue;
            bool ours=owner==player||wearer==player;
            for(int depth=0;!ours&&owner!=0&&depth<3;depth++)
            {if(!Host.TryGetObjectOwnershipInfo(owner,out owner,out wearer,out _))break;ours=owner==player||wearer==player;}
            if(!ours||!Host.TryGetObjectName(id,out string name))continue;
            session.ObserveChestReceipt(id,name,pair.Value.Chest);receiptIds[id]=now;chestItems.Remove(id);
        }
        foreach(var id in receiptIds.Where(x=>now-x.Value>TimeSpan.FromMinutes(1)).Select(x=>x.Key).ToArray())receiptIds.Remove(id);
        if(receiptIds.Count>1024)foreach(var id in receiptIds.OrderBy(x=>x.Value).Take(receiptIds.Count-1024).Select(x=>x.Key).ToArray())receiptIds.Remove(id);
    }
    private void ResetChestObservation(){chestItems.Clear();receiptIds.Clear();Volatile.Write(ref signaledChest,0);nextChestPoll=DateTime.MinValue;}
}

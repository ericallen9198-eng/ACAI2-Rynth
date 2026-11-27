using ACAI2.Rynth.Ui;
using RynthCore.Engine.UI.Sections;
int checks=0;
void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
foreach(var spec in new[]{new ToolbarWindow("scrollwork","Scrollwork","\uEB7A",680,540),new ToolbarWindow("threads","Threads","\uE6A2",460,500),new ToolbarWindow("marae","ACAI2 Marae","\uE31A",460,420)})
{
    var m=SectionManifestParser.Parse("ACAI2.Test",spec.Json,out string error);
    Check(error.Length==0&&m.Windows.Length==1,"genuine engine parser accepts the toolbar declaration");
    var w=m.Windows.Single();Check(w.Bar&&w.Title==spec.Title&&w.Surface==spec.Surface&&w.Icon.Length==1,"button, icon and surface survive actual parser");
    Check(!w.TakeOver,"no replacement of any built-in engine panel");
    int draws=0;Check(spec.Draw(spec.Surface,500,300,()=>draws++)==1&&draws==1,"hosted surface draws existing body");
    Check(spec.Draw(spec.Surface+":popout",500,300,()=>draws++)==1&&draws==2,"pop-out surface supported without replacing its context");
    Check(spec.Draw("window:unrelated",500,300,()=>draws++)==0&&draws==2,"unknown surface cannot draw or issue callbacks");
    Check(spec.Draw(spec.Surface,float.NaN,300,()=>draws++)==0&&draws==2,"invalid dimensions refused");
    Check(spec.Draw(spec.Surface,500,300,()=>throw new Exception())==-2,"render exception is contained at ABI boundary");
}
var visibility=new ToolbarVisibility(new ToolbarWindow("test","Test","\uEB7A",400,300));var native=new List<int>();bool? visible=null;
int Native(string key,int op){native.Add(op);return op==-1?0:op;}
visibility.Sync(Native,v=>visible=v);Check(native.SequenceEqual(new[]{-1})&&visible==false,"engine open state is authoritative; legacy default cannot reopen a closed panel");
visibility.Request(true);visibility.Sync((_,op)=>-1,v=>visible=v);visibility.Sync(Native,v=>visible=v);
Check(native.Last()==1&&visible==true,"slash-command show retries until hosted window is registered");
visibility.Sync(Native,v=>visible=v);Check(native.Last()==-1&&visible==false,"toolbar close updates visibility without restarting a workflow");
visibility.Request(true);visibility.Reset();visibility.Sync(Native,v=>visible=v);Check(native.Last()==-1,"logout clears an old session's pending show");
Console.WriteLine($"Toolbar checks passed: {checks}. Genuine parser; no engine edits or game actions.");

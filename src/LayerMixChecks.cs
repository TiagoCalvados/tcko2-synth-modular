using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace TCKO2Synth {
partial class MainForm {
    internal void VerifyFullSavedMix(StringBuilder report,string existing=null){
        if(existing==null){Rack.ResetLayers(0);for(int index=1;index<4;index++)Rack.SetPatch(index);foreach(int index in new[]{1,3,6})Rack.SelectTechnology(index);Rack.AddElectro(11);RefreshLayers();SaveSession();}else sessionOverride=existing;LoadSession();Engine.SetHold(false);Engine.SetDrone(false);
        if(Rack.Layers.Length!=8)throw new Exception("Expected a saved eight-layer mix");var originals=Rack.Layers;Engine.NoteOn(48,100,0);Engine.NoteOn(60,100,0);
        for(int index=0;index<12;index++){technologyChoice.SelectedIndex=index;UseTechnology();RefreshLayers();if(Rack.TechnologyLayer.Index!=index||technologyChoice.SelectedIndex!=index||originals.Any(x=>!Rack.Layers.Contains(x)))throw new Exception("Saved mix blocked technology selection "+index);}
        for(int index=0;index<8;index++){electroChoice.SelectedIndex=index;RefreshLayers();if(Rack.ElectroLayer.Index!=index+11||electroChoice.SelectedIndex!=index||originals.Any(x=>!Rack.Layers.Contains(x)))throw new Exception("Saved mix blocked Old Electro selection "+index);}
        if(Rack.Layers.Any(x=>x.On&&(!x.Voice.IsNote(48)||!x.Voice.IsNote(60))))throw new Exception("Saved-mix additions lost held notes");
        if(layerCount.Text!="LAYERS "+Rack.Layers.Length+"/31")throw new Exception("Layer capacity must be visible");Panic();Rack.ResetLayers(0);RefreshLayers();
        report.AppendLine("PASS: "+(existing==null?"synthetic saved mix":"actual saved session copy")+" with eight occupied slots allows all twelve technologies and eight Old Electro sounds; previous sounds, held notes and visible count are preserved");
    }
    [DllImport("user32.dll",EntryPoint="SendMessage",CharSet=CharSet.Auto)]
    static extern IntPtr SendSelectorMessage(IntPtr window,int message,IntPtr value,IntPtr unused);
    sealed class SelectionMessages:NativeWindow {
        internal int Resets;
        protected override void WndProc(ref Message message){if(message.Msg==0x14e)Resets++;base.WndProc(ref message);}
    }
    internal void VerifySelectorMenus(StringBuilder report){
        Rack.ResetLayers(0);Engine.NoteOn(48,100,0);technologyChoice.SelectedIndex=2;UseTechnology();electroChoice.SelectedIndex=1;Rack.AddElectro(12);RefreshLayers();
        foreach(var choice in new[]{boxGlitchChoice,technologyChoice,electroChoice,layerList}){
            var probe=new SelectionMessages();probe.AssignHandle(choice.Handle);
            try{choice.DroppedDown=true;Application.DoEvents();int selected=(choice.SelectedIndex+1)%choice.Items.Count;
                // The native list highlights a pending item before CBN_SELENDOK commits it.
                // Set that native state directly, without the managed change event.
                SendSelectorMessage(choice.Handle,0x14e,(IntPtr)selected,IntPtr.Zero);probe.Resets=0;
                for(int tick=0;tick<30;tick++){RefreshLayers();Application.DoEvents();}
                if(!choice.DroppedDown||choice.SelectedIndex!=selected||probe.Resets!=0)throw new Exception("Live refresh reset an open selector: "+choice.AccessibleName+" (native selection writes: "+probe.Resets+")");
                choice.DroppedDown=false;RefreshLayers();probe.Resets=0;for(int tick=0;tick<30;tick++)RefreshLayers();
                if(probe.Resets!=0)throw new Exception("Idle refresh rewrote a settled selector: "+choice.AccessibleName);
            }finally{choice.DroppedDown=false;probe.ReleaseHandle();}
        }
        for(int index=0;index<glitchTypes.Length;index++){boxGlitchChoice.SelectedIndex=index;RefreshLayers();if((int)Engine.SelectedGlitchType!=index||!Engine.IsNote(48))throw new Exception("Glitch choice did not commit");}
        for(int index=0;index<12;index++){Rack.ResetLayers(0);Engine.NoteOn(48,100,0);technologyChoice.SelectedIndex=(index+1)%12;UseTechnology();var previous=Rack.TechnologyLayer;technologyChoice.SelectedIndex=index;UseTechnology();RefreshLayers();if(Rack.TechnologyLayer.Index!=index||!Rack.Layers.Contains(previous)||!Rack.Layers.All(x=>x.Voice.IsNote(48)))throw new Exception("Technology selection or retained sound failed: "+index);}
        for(int index=0;index<8;index++){Rack.ResetLayers(0);Engine.NoteOn(48,100,0);electroChoice.SelectedIndex=(index+1)%8;Rack.AddElectro((index+1)%8+11);var previous=Rack.ElectroLayer;electroChoice.SelectedIndex=index;RefreshLayers();if(Rack.ElectroLayer.Index!=index+11||!Rack.Layers.Contains(previous)||!Rack.Layers.All(x=>x.Voice.IsNote(48)))throw new Exception("Old Electro selection or retained sound failed: "+index);}
        Panic();Rack.ResetLayers(0);RefreshLayers();
        report.AppendLine("PASS: open menus survive live timer refresh without native selection resets; every Glitch, Technology and Old Electro choice commits while previous layers keep held notes");
    }
    internal void VerifyLayerMixUI(){
        Rack.ResetLayers(0);RefreshLayers();var first=Rack.Layers[0];Engine.NoteOn(60,100,0);
        boxGlitchChoice.SelectedIndex=1;mixGlitch.Checked=true;if(!Engine.GlitchMode||!first.Voice.IsNote(60))throw new Exception("Glitch must affect the playing keyboard mix");
        technologyChoice.SelectedIndex=2;UseTechnology();electroChoice.SelectedIndex=5;Rack.AddElectro(16);RefreshLayers();
        if(Rack.Layers.Length!=3||!first.Voice.IsNote(60)||!Rack.Layers.All(x=>x.Voice.IsNote(60)))throw new Exception("Sound sections must stack on the held note");
        technologyChoice.SelectedIndex=6;UseTechnology();if(Rack.Layers.Length!=4||!Rack.Layers.Any(x=>x.Kind==1&&x.Index==2))throw new Exception("Earlier technology was replaced");
        Router.Handle(0x90|(64<<8)|(100<<16));if(!Rack.Layers.All(x=>x.Voice.IsNote(64)))throw new Exception("MIDI must reach every kept layer");
        var keep=Rack.Layers.Last();layerList.SelectedItem=first;removeLayer.PerformClick();if(Rack.Layers.Length!=3||!keep.Voice.IsNote(60)||!keep.Voice.IsNote(64))throw new Exception("Removing one layer interrupted another");
        short[] pcm=new short[1024];volume.Value=0;Engine.Render(pcm,512);if(pcm.Any(x=>x!=0))throw new Exception("Master mute must cover the shared mix");volume.Value=30;Panic();Rack.ResetLayers(0);RefreshLayers();
    }
    internal void VerifyLayerLayout(string folder,StringBuilder report){
        sessionOverride=Path.Combine(folder,"layer-session.txt");var handle=Handle;var bounds=Bounds;VerifyLayerMixUI();
        Engine.NoteOn(48,100,0);technologyChoice.SelectedIndex=2;UseTechnology();electroChoice.SelectedIndex=0;Rack.AddElectro(11);presets.SelectedIndex=3;RefreshLayers();
        if(Rack.Layers.Length!=4||Rack.Layers.Any(x=>!x.Voice.IsNote(48))||Handle!=handle||Bounds!=bounds)throw new Exception("Adding sound layers must preserve notes and the window");
        SaveSession();int count=Rack.Layers.Length;LoadSession();short[] pcm=new short[512];Engine.Render(pcm,256);if(Rack.Layers.Length!=count||pcm.Any(x=>x!=0))throw new Exception("Layer session must restore silently");
        using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"layered-synth.png"));}
        report.AppendLine("PASS: sound selectors stack immediately on held keys; old sounds, MIDI, remove, master, silent session restoration and window geometry are preserved");
    }
}
static class LayerMixChecks {
    static void Check(bool value,string message,StringBuilder report){if(!value)throw new Exception(message);report.AppendLine("PASS: "+message);}
    public static void Run(string folder){Directory.CreateDirectory(folder);var report=new StringBuilder();try{
        var rack=new LayeredSynth();rack.SetHold(true);rack.NoteOn(48,100,0);rack.NoteOff(48,0);var first=rack.Layers[0];short[] pcm=new short[512];rack.Render(pcm,256);rack.SelectTechnology(2);rack.AddElectro(11);rack.SetPatch(3);Check(rack.Layers.Length==4&&rack.Layers.All(x=>x.Voice.IsNote(48))&&ReferenceEquals(first,rack.Layers[0]),"Velvet Voltage remains while Frozen Spectrum, Old Electro and another main preset are added",report);
        rack.SelectTechnology(6);Check(rack.Layers.Length==5&&rack.Layers.Any(x=>x.Kind==1&&x.Index==2),"Selecting Wavefold keeps Frozen Spectrum and every previous sound",report);
        rack.NoteOn(60,100,1);Check(rack.Layers.All(x=>x.Voice.IsNote(60)),"New keyboard notes reach all active layers",report);rack.SetHold(false);rack.NoteOff(60,1);Check(!rack.IsNote(48)&&!rack.IsNote(60),"Note-off and sustain release every layer",report);rack.Panic();int kept=rack.Layers.Length;rack.Render(pcm,256);Check(kept==5&&pcm.All(x=>x==0),"Panic clears sound but retains the configured layer stack",report);
        var saved=new List<string>();rack.SaveLayers(saved);var restored=new LayeredSynth();Check(restored.RestoreLayers(saved.ToArray())&&restored.Layers.Length==5,"All selected sound layers survive a session round trip",report);restored.Render(pcm,256);Check(pcm.All(x=>x==0),"Restoring a stack never autoplays notes",report);
        var dry=new LayeredSynth();var wet=new LayeredSynth();foreach(var item in new[]{dry,wet}){item.SetPatch(3);item.SelectTechnology(6);item.AddElectro(12);item.NoteOn(48,100,0);}wet.SetGlitchType(GlitchType.Crush);wet.SetGlitchAmount(1);wet.SetGlitch(true);var other=new short[512];double difference=0;
        for(int block=0;block<120;block++){dry.Render(pcm,256);wet.Render(other,256);for(int i=0;i<512;i++)difference+=Math.Abs((double)pcm[i]-other[i]);}Check(difference>10000,"Glitch changes the combined waveform rather than starting a separate fixed note",report);
        string record=Path.Combine(folder,"layered-performance.wav");using(var rec=new TCKAudio.PcmRecorder()){rec.Start(record);wet.Render(pcm,256);rec.Append(pcm,256);rec.Stop();}byte[] expected=new byte[1024];Buffer.BlockCopy(pcm,0,expected,0,1024);Check(File.ReadAllBytes(record).Skip(44).SequenceEqual(expected),"REC receives the complete layered and glitched PCM mix",report);
        wet.Volume(0);wet.Render(pcm,256);Check(pcm.All(x=>x==0),"Master mute includes every layer and Glitch",report);wet.Volume(.3);wet.FadeOut(1);for(int i=0;i<180;i++)wet.Render(pcm,256);Check(wet.Faded&&pcm.All(x=>x==0),"Fade reaches silence across all layers and effects",report);
        var capped=new LayeredSynth();for(int i=1;i<11;i++)capped.SetPatch(i);for(int i=0;i<12;i++)capped.SelectTechnology(i);for(int i=11;i<19;i++)capped.AddElectro(i);var references=capped.Layers;
        for(int i=0;i<12;i++){capped.SelectTechnology(i);Check(capped.TechnologyLayer.Index==i&&capped.Layers.SequenceEqual(references),"All catalogue slots occupied: technology "+i+" remains selectable",report);}
        capped.SetDrone(false);capped.NoteOn(48,100,0);capped.NoteOn(60,100,0);capped.NoteOn(64,100,0);Check(!capped.IsNote(64)&&capped.Layers.All(x=>x.Voice.IsNote(48)&&x.Voice.IsNote(60)),"Full catalogue shares two held keys; extra notes leave the existing mix intact",report);
        var watch=Stopwatch.StartNew();for(int i=0;i<1723;i++)capped.Render(pcm,256);watch.Stop();Check(watch.Elapsed.TotalSeconds<8&&capped.InvalidSamples==0,"Thirty-one layers / 62 held voices render ten seconds in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s with finite bounded output",report);
        var allSaved=new List<string>();capped.SaveLayers(allSaved);var allRestored=new LayeredSynth();allRestored.RestoreLayers(allSaved.ToArray());allRestored.Render(new short[1024],512);Check(allRestored.Layers.Length==31&&allRestored.ActiveCount==0,"The full catalogue restores silently with a larger audio block",report);
        capped.Panic();for(int step=0;step<5;step++){int note=48+step*2;capped.NoteOn(note,100,0);capped.NoteOn(note+7,100,0);capped.Render(pcm,256);Check(capped.ActiveCount<=64&&capped.Layers.All(x=>x.Voice.IsNote(note)),"Rapid full-catalogue chord change stays within the shared render budget: "+step,report);capped.NoteOff(note,0);capped.NoteOff(note+7,0);}
        var parallel=new LayeredSynth();var serial=new LayeredSynth();((System.Threading.Tasks.ParallelOptions)typeof(LayeredSynth).GetField("renderOptions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(serial)).MaxDegreeOfParallelism=1;foreach(var item in new[]{parallel,serial}){item.SetPatch(3);item.SelectTechnology(6);item.AddElectro(12);item.NoteOn(48,100,0);item.NoteOn(60,100,0);}for(int block=0;block<50;block++){parallel.Render(pcm,256);serial.Render(other,256);Check(pcm.SequenceEqual(other),"Parallel voices preserve serial PCM: "+block,report);}
        var waiting=new LayeredSynth();for(int i=1;i<8;i++)waiting.SetPatch(i);waiting.SetDrone(false);for(int note=48;note<56;note++)waiting.NoteOn(note,100,0);waiting.SelectTechnology(2);var pending=waiting.TechnologyLayer;Check(pending.Pending&&waiting.Layers.Length==9&&waiting.Layers.Take(8).All(x=>x.Voice.IsNote(48)),"A full held chord accepts a new sound as waiting without replacing any layer",report);waiting.NoteOff(48,0);Check(!pending.Pending&&pending.Voice.IsNote(49)&&waiting.Layers.Take(8).All(x=>x.Voice.IsNote(49)),"Waiting selection joins automatically when enough keys are released",report);
        using(var form=new MainForm(true)){form.Show();Application.DoEvents();form.VerifySelectorMenus(report);form.VerifyLayerLayout(folder,report);form.VerifyFullSavedMix(report);form.Close();}string existing=Path.Combine(folder,"existing-session.txt");if(File.Exists(existing))using(var form=new MainForm(true)){form.Show();Application.DoEvents();form.VerifyFullSavedMix(report,existing);form.Close();}report.AppendLine("No live MIDI or audio device opened. RESULT: PASS");
    }catch(Exception error){report.AppendLine(error.ToString());throw;}finally{File.WriteAllText(Path.Combine(folder,"layer-mix-verification.txt"),report.ToString());}}
}
}


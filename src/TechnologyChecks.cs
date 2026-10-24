using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
namespace TCKO2Synth {
partial class MainForm {
 internal void VerifyTechnologies(string folder,StringBuilder report){
  sessionOverride=Path.Combine(folder,"test-session.txt");short[] pcm=new short[512];
  if(technologyChoice.Items.Count!=12||electroChoice.Items.Count!=8||presets.Items.Count!=11)throw new Exception("Lists changed");
  Control center=technologyChoice.Parent.Parent;TableLayoutPanel row=(TableLayoutPanel)center.Parent;if(row.GetColumn(center)!=1||row.GetColumn(boxGlitchChoice.Parent.Parent)!=0||row.GetColumn(electroChoice.Parent.Parent)!=2)throw new Exception("Box order");
  var handle=Handle;var bounds=Bounds;boxes.Play(false,true);boxes.Play(true,true);
  for(int type=0;type<12;type++){
   Engine.Panic();technologyChoice.SelectedIndex=type;UseTechnology();Engine.Render(pcm,256);if(pcm.Any(x=>x!=0))throw new Exception("Selection must be silent");
   piano.Press(48);Engine.Render(pcm,256);if(pcm.All(x=>x==0)||!Engine.IsNote(48))throw new Exception("Screen key must play technology "+type);piano.Release(48);Engine.Panic();
   KeyPressed(this,new KeyEventArgs(Keys.A));Engine.Render(pcm,256);if(pcm.All(x=>x==0))throw new Exception("Computer key must play");KeyReleased(this,new KeyEventArgs(Keys.A));Engine.Panic();heldKeys.Clear();
   Router.Handle(0x90|(60<<8)|(100<<16));Router.Handle(0x90|(64<<8)|(100<<16));for(int blocks=0;blocks<12;blocks++)Engine.Render(pcm,256);if(!Engine.IsNote(60)||!Engine.IsNote(64)||pcm.All(x=>x==0))throw new Exception("MIDI chord must play "+type+" notes "+Engine.IsNote(60)+","+Engine.IsNote(64)+" peak "+Engine.Peak);Router.Handle(0x80|(60<<8));if(Engine.IsNote(60)||!Engine.IsNote(64))throw new Exception("Independent note release");Engine.Panic();
   if(!boxes.Glitch.IsNote(60)||!boxes.Electro.IsNote(60)||Handle!=handle||Bounds!=bounds)throw new Exception("Other sources or window changed");
  }
  technologyChoice.SelectedIndex=5;technologyCharacter.Value=71;technologyLevel.Value=42;SaveSession();technologyChoice.SelectedIndex=0;LoadSession();Engine.Render(pcm,256);if(Engine.TechnologyType!=5||technologyCharacter.Value!=71||technologyLevel.Value!=42||pcm.Any(x=>x!=0))throw new Exception("Restore must retain selected keyboard sound silently");
  ToggleStop();piano.Press(48);Engine.Render(pcm,256);if(pcm.Any(x=>x!=0))throw new Exception("Global Stop");ToggleStop();Panic();
  using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(folder,"technologies-desktop.png"));}
  report.AppendLine("PASS: ordered boxes, silent selection, on-screen/computer/MIDI keys, chords, note-off, independent boxes, stable window, silent settings restore, Stop and Panic");
 }
}
static class TechnologyChecks {
 public static void Run(string folder){Directory.CreateDirectory(folder);var report=new StringBuilder();try{
  var v=new TechnologyVoice(44100);var synth=new Synth();var router=new MidiRouter(synth);short[] pcm=new short[512];
  for(int type=0;type<12;type++){
   v.Configure(type,48,.5,.35);v.Panic();v.Play(true);double energy=0,peak=0;
   using(var dump=new BinaryWriter(System.IO.File.Create(Path.Combine(folder,"technology-"+type+".pcm"))))for(int i=0;i<44100*3;i++){v.Next();if(Double.IsNaN(v.Left)||Double.IsInfinity(v.Left)||Double.IsNaN(v.Right)||Double.IsInfinity(v.Right)||Math.Abs(v.Left)>.8||Math.Abs(v.Right)>.8)throw new Exception("Invalid audio");energy+=v.Left*v.Left+v.Right*v.Right;peak=Math.Max(peak,Math.Max(Math.Abs(v.Left),Math.Abs(v.Right)));if(i<44100){dump.Write(v.Left);dump.Write(v.Right);}}if(energy<.001||peak>=.8)throw new Exception("Silent or clipped default texture");
   synth.SelectTechnology(type);synth.Panic();synth.Render(pcm,256);if(pcm.Any(x=>x!=0))throw new Exception("Autoplay on selection");router.Handle(0x90|(48<<8)|(100<<16));energy=0;for(int b=0;b<30;b++){synth.Render(pcm,256);energy+=pcm.Sum(x=>(double)x*x);}if(energy==0)throw new Exception("Keyboard silent "+type);router.Handle(0x80|(48<<8));for(int b=0;b<100;b++)synth.Render(pcm,256);if(synth.IsNote(48))throw new Exception("Release failed");synth.Panic();synth.Render(pcm,256);if(pcm.Any(x=>x!=0))throw new Exception("Panic failed");
   synth.SetHold(true);synth.NoteOn(48,100,0);synth.NoteOff(48,0);if(!synth.IsNote(48))throw new Exception("Sustain");synth.SetHold(false);if(synth.IsNote(48))throw new Exception("Sustain release");synth.Panic();
   report.AppendLine("PASS: "+TechnologyNames.Names[type]+" distinct bounded signal, silent selection, keyboard note, sustain/release, Panic");
  }
  synth.SelectTechnology(5);synth.Panic();synth.NoteOn(48,100,0);string recorded=Path.Combine(folder,"keys-"+Guid.NewGuid().ToString("N")+".wav");using(var rec=new TCKAudio.PcmRecorder()){rec.Start(recorded);for(int i=0;i<30;i++){synth.Render(pcm,256);rec.Append(pcm,256);}rec.Stop();}if(!Wav.Read(recorded).Samples.Any(x=>x!=0))throw new Exception("REC empty");
  synth.Panic();synth.NoteOn(48,100,0);synth.FadeOut(1);for(int i=0;i<200;i++)synth.Render(pcm,256);if(pcm.Any(x=>x!=0))throw new Exception("Fade");
  var watch=Stopwatch.StartNew();synth.Panic();synth.SelectTechnology(8);for(int n=0;n<8;n++)synth.NoteOn(48+n*3,127,0);for(int b=0;b<1723;b++)synth.Render(pcm,256);watch.Stop();if(synth.InvalidSamples!=0||watch.Elapsed.TotalSeconds>5)throw new Exception("Polyphonic render budget");report.AppendLine("PASS: REC includes keyboard technology, Fade; eight-voice convolution ten seconds in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s");
  using(var form=new MainForm(true)){form.Show();Application.DoEvents();form.VerifyTechnologies(folder,report);form.Close();}report.AppendLine("No live MIDI or audio opened. RESULT: PASS");
 }catch(Exception e){report.AppendLine(e.ToString());throw;}finally{System.IO.File.WriteAllText(Path.Combine(folder,"technologies-verification.txt"),report.ToString());}}
}
}

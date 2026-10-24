using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace TCKO2Synth {
partial class MainForm {
    internal void VerifySoundFiles(string folder,StringBuilder report) {
        Action<bool,string> check=(yes,message)=>{if(!yes)throw new Exception(message);report.AppendLine("PASS: "+message);};
        string path=Path.Combine(SoundImport.Folder,"Future sound.wav");
        using(var file=System.IO.File.Create(path))Wav.Write(file,4410,(f,c)=>.3*Math.Sin(f*Math.PI*440/44100)*(c==0 ? 1:-.5));
        byte[] before=System.IO.File.ReadAllBytes(path);IntPtr handle=Handle;Rectangle location=Bounds;
        check(presets.Items.Count==11 && electroChoice.Items.Count==8,"Upper selector keeps exactly eleven sounds and Old Electro keeps all eight originals");
        check(openSound.Visible && openSound.Top==0 && openSound.Parent==midiStatus.Parent && openSound.Right<=midiStatus.Left,"Open sound is visible in the title header beside the synth name");
        SoundPicker=(dialog,owner)=>{check(dialog.InitialDirectory==SoundImport.Folder && ReferenceEquals(owner,this),"Open sound starts in TCK02 SOUNDS and belongs to the synth window");dialog.FileName=path;return DialogResult.OK;};
        openSound.PerformClick();WaitSound();check(boxes.File.Playing && boxes.File.Clip.Name=="Future sound","Open sound loads and starts the selected file");
        check(Handle==handle && Bounds==location && Visible,"Loading a file preserves the synth window and its position");
        Engine.NoteOn(60,90,0);boxes.Play(false,true);short[] block=new short[1024];boxes.Mix(block,512);
        check(block.Any(x=>x!=0) && Engine.IsNote(60) && boxes.Electro.IsNote(60),"Loaded file mixes without resetting synth or Old Electro notes");
        long held=boxes.File.Position;SoundPicker=(dialog,owner)=>DialogResult.Cancel;openSound.PerformClick();
        check(boxes.File.Playing && boxes.File.Position==held,"Cancelling keeps the current file and playback position");
        string bad=Path.Combine(folder,"broken.wav");System.IO.File.WriteAllText(bad,"not audio");LoadSoundFile(bad).GetAwaiter();WaitUntil(()=>!loadingSound);
        check(boxes.File.Clip.Name=="Future sound" && boxes.File.Playing,"Failed loading retains the current file");
        stopSound.PerformClick();check(!boxes.File.Playing && Engine.IsNote(60) && boxes.Electro.IsNote(60),"Stop sound stops the file while existing synth voices keep playing");
        playSound.PerformClick();Panic();check(!boxes.File.Playing && !Engine.IsNote(60) && !boxes.ElectroOn,"Panic stops file and synth audio together");
        PendingSound=LoadSoundFile(path);Panic();WaitSound();check(!boxes.File.Playing && boxes.File.Clip!=null,"Panic during decoding prevents delayed autoplay");
        volume.Value=0;playSound.PerformClick();Array.Clear(block,0,block.Length);boxes.Mix(block,512);check(block.All(x=>x==0),"Master volume mutes imported sound");
        check(System.IO.File.ReadAllBytes(path).SequenceEqual(before),"Opening, playing and stopping leave the original file unchanged");
        string session=Path.Combine(folder,"legacy-session.txt");System.IO.File.WriteAllLines(session,new[] {"preset=18","boxElectro=7","master=20"});sessionOverride=session;LoadSession();sessionOverride=null;
        check(presets.SelectedIndex==0 && electroChoice.SelectedIndex==7,"Old saved duplicate selections reopen safely with the Old Electro choice preserved");
        using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"synth-open-sound.png"));}
        Panic();
    }
    void WaitSound(){WaitUntil(()=>PendingSound.IsCompleted);PendingSound.GetAwaiter().GetResult();}
    static void WaitUntil(Func<bool> done){var timer=System.Diagnostics.Stopwatch.StartNew();while(!done() && timer.ElapsedMilliseconds<30000){Application.DoEvents();Thread.Sleep(1);}if(!done())throw new Exception("Sound load timed out");}
}
static class SoundFileChecks {
    public static void Run(string folder) {
        Directory.CreateDirectory(folder);SoundImport.Folder=Path.Combine(folder,"SOUNDS");Directory.CreateDirectory(SoundImport.Folder);var report=new StringBuilder();
        try {
            using(var form=new MainForm(true)){form.Show();Application.DoEvents();form.VerifySoundFiles(folder,report);form.Close();}
            var clip=new AudioClip("mix fixture",Enumerable.Repeat(.25f,4410*2).ToArray());var boxes=new SoundBoxes();boxes.Master=.5;boxes.File.Load(clip,true);short[] pcm=new short[512];
            string record=Path.Combine(folder,"imported-performance.wav");using(var recorder=new TCKAudio.PcmRecorder()){recorder.Start(record);boxes.Mix(pcm,256);recorder.Append(pcm,256);recorder.Stop();}
            byte[] expected=new byte[pcm.Length*2];Buffer.BlockCopy(pcm,0,expected,0,expected.Length);
            if(!System.IO.File.ReadAllBytes(record).Skip(44).SequenceEqual(expected) || pcm.All(x=>x==0))throw new Exception("Imported PCM not captured in performed recording");report.AppendLine("PASS: Imported file is mixed before the common REC tap");
            string codecs=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"codecs");
            foreach(string file in Directory.Exists(codecs)?Directory.GetFiles(codecs):new string[0]) {
                byte[] before=System.IO.File.ReadAllBytes(file);var decoded=SoundImport.Read(file);
                if(decoded.Frames==0 || decoded.Samples.All(x=>x==0) || !System.IO.File.ReadAllBytes(file).SequenceEqual(before))throw new Exception("Codec import failed: "+file);
                report.AppendLine("PASS: "+Path.GetExtension(file)+" file decodes locally without modifying the original");
            }
            report.AppendLine("No live MIDI input or audio device opened. RESULT: PASS");
        } catch(Exception error){report.AppendLine(error.ToString());report.AppendLine("RESULT: FAIL");throw;}
        finally{System.IO.File.WriteAllText(Path.Combine(folder,"files-verification.txt"),report.ToString());}
    }
}
}

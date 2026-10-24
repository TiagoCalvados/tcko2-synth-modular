using System;
using System.IO;

namespace TCKO2Synth {
static class SynthHome {
    internal static string Folder {get {string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SettingsHome.txt");if(System.IO.File.Exists(config)){string value=System.IO.File.ReadAllText(config).Trim();if(Path.IsPathRooted(value) && !value.StartsWith(@"\\") && Directory.Exists(value))return value;}return AppDomain.CurrentDomain.BaseDirectory;}}
}
static class SoundImport {
    internal static string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Projects","TCK02","SOUNDS");
    internal const string Filter="Sound files (WAV, M4A, MP3, MP4 audio)|*.wav;*.m4a;*.mp3;*.mp4|WAV audio|*.wav|M4A / MP4 audio|*.m4a;*.mp4|MP3 audio|*.mp3";
    internal static AudioClip Read(string path) {
        path=Path.GetFullPath(path);if(path.StartsWith(@"\\"))throw new IOException("Choose a local sound file.");
        string extension=Path.GetExtension(path).ToLowerInvariant();
        if(extension==".wav")return Wav.Read(path);
        if(extension==".m4a" || extension==".mp3" || extension==".mp4")return MediaFileDecoder.Read(path);
        throw new IOException("Choose WAV, M4A, MP3, or MP4 with audio.");
    }
}
sealed class SoundFilePlayer {
    readonly object gate=new object();AudioClip clip;long position;bool playing;double fade=1,fadeStep;
    public bool Playing {get{lock(gate)return playing;}}
    public AudioClip Clip {get{lock(gate)return clip;}}
    public long Position {get{lock(gate)return position;}}
    public void Load(AudioClip value,bool play) {lock(gate){clip=value;position=0;playing=play && value!=null;fade=1;fadeStep=0;}}
    public void Play(){lock(gate){position=0;playing=clip!=null;fade=1;fadeStep=0;}}
    public void Stop(){lock(gate){playing=false;position=0;fade=1;fadeStep=0;}}
    public void FadeOut(int seconds){lock(gate){fadeStep=1.0/(Math.Max(1,seconds)*Wav.Rate);}}
    public void Mix(short[] output,int frames,double master) {
        lock(gate){if(!playing || clip==null)return;
            for(int frame=0;frame<frames;frame++) {
                if(position>=clip.Frames || fade<=0){playing=false;break;}
                for(int channel=0;channel<2;channel++) {
                    int at=frame*2+channel;double value=output[at]+clip.Samples[position*2+channel]*32767*Math.Max(0,Math.Min(.8,master))*fade;
                    output[at]=(short)Math.Round(Math.Max(-30000,Math.Min(30000,value)));
                }
                position++;fade=Math.Max(0,fade-fadeStep);
            }
            if(position>=clip.Frames || fade<=0)playing=false;
        }
    }
}
}

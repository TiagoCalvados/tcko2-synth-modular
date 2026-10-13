using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("TCKO2 Synth modular")]
[assembly: System.Reflection.AssemblyDescription("Experimental Windows MIDI synthesizer")]
[assembly: System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.0.0")]
namespace TCKO2Synth {
static class Native {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct MidiCaps { public ushort mid,pid; public uint version; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string name; public uint support; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] public struct AudioCaps { public ushort mid,pid; public uint version; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string name; public uint formats; public ushort channels,reserved; public uint support; }
    [StructLayout(LayoutKind.Sequential,Pack=2)] public struct Format { public ushort tag,channels; public uint rate,bytes; public ushort align,bits,extra; }
    [StructLayout(LayoutKind.Sequential)] public struct Header { public IntPtr data; public uint length,recorded; public UIntPtr user; public uint flags,loops; public IntPtr next; public UIntPtr reserved; }
    public delegate void MidiCallback(IntPtr handle,uint message,UIntPtr instance,UIntPtr p1,UIntPtr p2);
    [DllImport("winmm.dll")] public static extern uint midiInGetNumDevs();
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] public static extern uint midiInGetDevCaps(UIntPtr id,out MidiCaps caps,uint size);
    [DllImport("winmm.dll")] public static extern uint midiInOpen(out IntPtr handle,uint id,MidiCallback callback,UIntPtr instance,uint flags);
    [DllImport("winmm.dll")] public static extern uint midiInStart(IntPtr h);
    [DllImport("winmm.dll")] public static extern uint midiInStop(IntPtr h);
    [DllImport("winmm.dll")] public static extern uint midiInReset(IntPtr h);
    [DllImport("winmm.dll")] public static extern uint midiInClose(IntPtr h);
    [DllImport("winmm.dll")] public static extern uint waveOutGetNumDevs();
    [DllImport("winmm.dll",CharSet=CharSet.Unicode)] public static extern uint waveOutGetDevCaps(UIntPtr id,out AudioCaps caps,uint size);
    [DllImport("winmm.dll")] public static extern uint waveOutOpen(out IntPtr h,uint id,ref Format f,IntPtr callback,UIntPtr instance,uint flags);
    [DllImport("winmm.dll")] public static extern uint waveOutPrepareHeader(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] public static extern uint waveOutUnprepareHeader(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] public static extern uint waveOutWrite(IntPtr h,IntPtr header,uint size);
    [DllImport("winmm.dll")] public static extern uint waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] public static extern uint waveOutClose(IntPtr h);
    public static string[] MidiNames() { List<string> r=new List<string>(); for(uint i=0;i<midiInGetNumDevs();i++) { MidiCaps c; if(midiInGetDevCaps(new UIntPtr(i),out c,(uint)Marshal.SizeOf(typeof(MidiCaps)))==0) r.Add(c.name); else r.Add("MIDI input "+i); } return r.ToArray(); }
    public static string[] AudioNames() { List<string> r=new List<string>(); r.Add("Windows default output"); for(uint i=0;i<waveOutGetNumDevs();i++) { AudioCaps c; if(waveOutGetDevCaps(new UIntPtr(i),out c,(uint)Marshal.SizeOf(typeof(AudioCaps)))==0) r.Add(c.name); else r.Add("Audio output "+i); } return r.ToArray(); }
}

class MidiInput : IDisposable {
    IntPtr handle; Native.MidiCallback callback;
    public readonly ConcurrentQueue<int> Messages=new ConcurrentQueue<int>();
    int count; public volatile bool Overflow;
    public string Name="";
    public void Open(int id,string name) { Dispose(); callback=Receive; uint e=Native.midiInOpen(out handle,(uint)id,callback,UIntPtr.Zero,0x30000); if(e!=0) {handle=IntPtr.Zero; throw new Exception("MIDI open error "+e+". Close other apps using this keyboard, then click Rescan.");} e=Native.midiInStart(handle); if(e!=0) {Dispose(); throw new Exception("MIDI start error "+e);} Name=name; }
    void Receive(IntPtr h,uint msg,UIntPtr instance,UIntPtr p1,UIntPtr p2) { if(msg!=0x3C3 && msg!=0x3CC) return; if(Interlocked.Increment(ref count)>4096) {Interlocked.Decrement(ref count); Overflow=true; return;} Messages.Enqueue(unchecked((int)p1.ToUInt64())); }
    public bool Pop(out int data) { if(Messages.TryDequeue(out data)) {Interlocked.Decrement(ref count); return true;} return false; }
    public void Dispose() { if(handle!=IntPtr.Zero) {Native.midiInStop(handle);Native.midiInReset(handle);Native.midiInClose(handle);handle=IntPtr.Zero;} Name=""; int n; while(Pop(out n)){} Overflow=false; }
}

class Patch {
    public string Name,Description; public double[] Values; public double Attack,Release,Decay,Sustain,Shape,EnvTone,EchoSeconds; public bool Drone;
    public Patch(string name,string desc,double[] values,double attack,double release,double decay,double sustain,double shape,double envTone,bool drone=false,double echoSeconds=.85) {Name=name;Description=desc;Values=values;Attack=attack;Release=release;Decay=decay;Sustain=sustain;Shape=shape;EnvTone=envTone;Drone=drone;EchoSeconds=echoSeconds;}
    public static readonly Patch[] All={
      new Patch("01  Velvet Voltage","Slow, warm twin oscillators. Let chords drift into the room.",new double[]{.46,.19,.55,.24,.31,.44,.02,.26},.055,.9,.6,.75,0,.2),
      new Patch("02  Cellar Acid","Rubbery pulse bass. Push Squelch and move Tone while playing.",new double[]{.19,.78,.18,.55,.28,.13,.0,.36},.006,.19,.22,.28,.75,2.8),
      new Patch("03  Dusty Polaroid","Soft vintage keys with a worn echo and a little wobble.",new double[]{.43,.24,.42,.4,.49,.32,.05,.47},.012,.65,.8,.3,.35,.6),
      new Patch("04  Copper Satellites","Inharmonic bells and orbital radio chatter. Explore Metal.",new double[]{.7,.32,.25,.2,.42,.52,.8,.32},.003,1.8,.8,.18,.2,.1),
      new Patch("05  Ghost Observatory","A slowly blooming sci-fi pad. Sustain a chord and raise Bloom.",new double[]{.36,.42,.8,.29,.66,.76,.35,.54},1.2,10,3,.95,0,.7,false,1.25),
      new Patch("06  Below the Floor / Drone","A deep, wide drone with long breaths. Try Bloom and a low fifth.",new double[]{.2,.24,.76,.4,.55,.64,.0,.16},2.0,12,4,.98,0,.3,true,1.6),
      new Patch("07  Lunar Array / Drone","Expansive orbital drone. Stack notes; let the long echoes orbit.",new double[]{.53,.5,.61,.25,.72,.83,.69,.4},3.5,16,4,1,.1,.3,true,2.2),
      new Patch("08  Rust Cathedral / Drone","A huge resonant drone. Add a fifth and let Bloom fill the room.",new double[]{.38,.58,.86,.48,.64,.9,.16,.3},2.8,14,3.5,1,.85,.4,true,1.8),
      new Patch("09  Broken Transistor Organ","Immediate vintage organ. Sustain latch keeps the chords under your hands.",new double[]{.62,.12,.18,.28,.14,.24,.0,.03},.004,.12,.2,1,.95,0),
      new Patch("10  Soot Ribbon Lead","A gritty, singing lead. Open Tone and add a little tape echo.",new double[]{.43,.53,.32,.72,.36,.18,.07,.14},.012,.32,.35,.72,.55,1.2),
      new Patch("11  Arcade Vapour","Hollow, shimmering keys. Turn Metal for a strange glassy edge.",new double[]{.66,.3,.25,.18,.46,.54,.38,.35},.015,1.25,1,.42,.8,.4)
    };
}
class Voice { public int Note,Channel; public long Age; public bool Active,Down,Held,Latched; public double P1,P2,P3,P4,Sub,Ring,Env,FilterEnv,Ic1,Ic2,Vel,Freq,Drift,Bloom; public int Stage; }
class Comb {
    double[] data; int pos; double damp;
    public Comb(int size){data=new double[size];}
    public double Tick(double input,double feedback) {double y=data[pos]; damp=.32*y+.68*damp;data[pos]=input+feedback*damp;if(++pos==data.Length)pos=0;return y;}
    public void Clear(){Array.Clear(data,0,data.Length);pos=0;damp=0;}
}
class Synth {
    public const int Rate=44100;
    readonly object gate=new object(); readonly Voice[] voices=new Voice[8]; readonly bool[] sustain=new bool[18]; readonly double[] bends=new double[18];
    readonly double[] target=new double[8],smooth=new double[8];
    double[] delayL=new double[Rate*5],delayR=new double[Rate*5],chorusL=new double[2048],chorusR=new double[2048]; int delayPos,chorusPos; double echoDampL,echoDampR,dcL,dcR,lastL,lastR,lfo,slowLfo,master=.2,masterSmooth=.2,bloomAmount=.75,bloomSmooth=.75,echoSeconds=.85,echoSecondsSmooth=.85;
    readonly Comb[] reverbL={new Comb(2591),new Comb(3251),new Comb(3907),new Comb(4451)};
    readonly Comb[] reverbR={new Comb(2677),new Comb(3371),new Comb(4003),new Comb(4561)};
    Patch patch=Patch.All[0]; long age; Random rng=new Random(947); public volatile bool Stopped; bool holdMode,droneMode;
    public bool HoldMode {get{lock(gate)return holdMode;}} public bool DroneMode {get{lock(gate)return droneMode;}}
    public volatile float Peak; public volatile int ActiveCount; public int InvalidSamples;
    public Synth(){for(int i=0;i<voices.Length;i++)voices[i]=new Voice();SetPatch(0);}
    public void SetPatch(int i){lock(gate){PanicCore();patch=Patch.All[i];droneMode=patch.Drone;bloomAmount=bloomSmooth=patch.Drone ? .9 : .75;echoSeconds=echoSecondsSmooth=patch.EchoSeconds;Array.Copy(patch.Values,target,8);Array.Copy(target,smooth,8);}}
    public void SetBloom(double value){lock(gate)bloomAmount=Math.Max(0,Math.Min(1,value));} public double BloomAmount{get{lock(gate)return bloomAmount;}}
    public void SetEchoTime(double seconds){lock(gate)echoSeconds=Math.Max(.08,Math.Min(4,seconds));} public double EchoTime{get{lock(gate)return echoSeconds;}}
    public void Set(int i,double v){lock(gate)target[i]=Math.Max(0,Math.Min(1,v));}
    public double Get(int i){lock(gate)return target[i];}
    public void Volume(double v){lock(gate)master=Math.Max(0,Math.Min(.8,v));}
    public void Bend(int ch,double semitones){lock(gate)bends[ch]=semitones;}
    public void NoteOn(int note,int velocity,int ch){if(velocity==0){NoteOff(note,ch);return;}lock(gate){if(Stopped)return;Voice chosen=null;foreach(Voice v in voices)if(v.Active&&v.Note==note&&v.Channel==ch){chosen=v;break;}if(droneMode&&chosen!=null&&chosen.Latched){chosen.Latched=chosen.Down=chosen.Held=false;chosen.Stage=2;return;}if(chosen==null)foreach(Voice v in voices)if(!v.Active){chosen=v;break;}if(chosen==null){chosen=voices[0];foreach(Voice v in voices)if(v.Age<chosen.Age)chosen=v;}chosen.Note=note;chosen.Channel=ch;chosen.Age=++age;chosen.Active=chosen.Down=true;chosen.Held=false;chosen.Latched=droneMode;chosen.Bloom=0;chosen.Stage=0;chosen.Env=0;chosen.FilterEnv=1;chosen.Ic1=chosen.Ic2=0;chosen.Vel=Math.Pow(velocity/127.0,.65);chosen.Freq=440*Math.Pow(2,(note-69)/12.0);chosen.Drift=rng.NextDouble()*2-1;chosen.P1=rng.NextDouble();chosen.P2=rng.NextDouble();chosen.P3=rng.NextDouble();chosen.P4=rng.NextDouble();chosen.Sub=rng.NextDouble();chosen.Ring=0;}}
    public void NoteOff(int note,int ch){lock(gate)foreach(Voice v in voices)if(v.Active&&v.Note==note&&v.Channel==ch){v.Down=false;if(v.Stage==2&&!v.Latched)continue;if(sustain[ch]||holdMode||v.Latched)v.Held=true;else v.Stage=2;}}
    void ReleaseUnheld(){foreach(Voice v in voices)if(v.Active&&!v.Down&&!v.Latched&&!holdMode&&!sustain[v.Channel]){v.Held=false;v.Stage=2;}}
    public void SetHold(bool enabled){lock(gate){holdMode=enabled;if(enabled){droneMode=false;foreach(Voice v in voices)v.Latched=false;}ReleaseUnheld();}}
    public void SetDrone(bool enabled){lock(gate){droneMode=enabled;if(enabled){holdMode=false;foreach(Voice v in voices)if(v.Active&&(v.Down||v.Held))v.Latched=true;}else foreach(Voice v in voices)v.Latched=false;ReleaseUnheld();}}
    public void Sustain(int ch,bool down){lock(gate){sustain[ch]=down;ReleaseUnheld();}}
    public void ReleaseChannel(int ch){lock(gate){sustain[ch]=false;foreach(Voice v in voices)if(v.Channel==ch){v.Down=v.Held=v.Latched=false;v.Stage=2;}}}
    public void ResetChannel(int ch){lock(gate){bends[ch]=0;Sustain(ch,false);}}
    public void ReleaseKeys(int ch){lock(gate)foreach(Voice v in voices)if(v.Channel==ch&&v.Down)NoteOff(v.Note,ch);}
    public bool IsNote(int note){lock(gate){foreach(Voice v in voices)if(v.Active&&(v.Down||v.Held)&&v.Note==note)return true;return false;}}
    public void Panic(){lock(gate)PanicCore();}
    void PanicCore(){holdMode=droneMode=false;foreach(Voice v in voices){v.Active=v.Down=v.Held=v.Latched=false;v.Env=v.Ic1=v.Ic2=v.Bloom=0;}Array.Clear(sustain,0,sustain.Length);Array.Clear(bends,0,bends.Length);Array.Clear(delayL,0,delayL.Length);Array.Clear(delayR,0,delayR.Length);Array.Clear(chorusL,0,chorusL.Length);Array.Clear(chorusR,0,chorusR.Length);foreach(Comb c in reverbL)c.Clear();foreach(Comb c in reverbR)c.Clear();echoDampL=echoDampR=dcL=dcR=lastL=lastR=0;Peak=0;ActiveCount=0;}
    static double Sat(double x){if(x>3)return 1;if(x< -3)return -1;double xx=x*x;return x*(27+xx)/(27+9*xx);}
    static double Blep(double p,double d){if(p<d){double t=p/d;return t+t-t*t-1;}if(p>1-d){double t=(p-1)/d;return t*t+t+t+1;}return 0;}
    static double Osc(double p,double d,double shape){double saw=2*p-1-Blep(p,d);double width=.42;double shifted=p-width;if(shifted<0)shifted+=1;double pulse=(p<width?1:-1)+Blep(p,d)-Blep(shifted,d);return saw*(1-shape)+pulse*shape;}
    static double ReadRing(double[] b,int pos,double distance){double p=pos-distance;while(p<0)p+=b.Length;while(p>=b.Length)p-=b.Length;int a=(int)p;double f=p-a;return b[a]*(1-f)+b[(a+1)%b.Length]*f;}
    double ReadDelay(double[] b,double distance){return ReadRing(b,delayPos,distance);}
    public void Render(short[] pcm,int frames){lock(gate){double peak=0;int active=0;foreach(Voice v in voices)if(v.Active)active++;ActiveCount=active;
      double attackStep=1/(Rate*patch.Attack),decayMul=Math.Exp(-1/(Rate*patch.Decay)),releaseMul=Math.Exp(-7/(Rate*patch.Release)),filterMul=Math.Exp(-1/(Rate*patch.Decay));
      double g=0,k=0,detune=0,drive=0,ringRatio=0,echo=0,space=0,motion=0;
      for(int n=0;n<frames;n++){
        if((n&15)==0){for(int i=0;i<8;i++)smooth[i]+=.012*(target[i]-smooth[i]);bloomSmooth+=.006*(bloomAmount-bloomSmooth);echoSecondsSmooth+=.0015*(echoSeconds-echoSecondsSmooth);masterSmooth+=.025*(master-masterSmooth);double cutoff=80*Math.Pow(150,smooth[0]);g=Math.Tan(Math.PI*Math.Min(16000,cutoff)/Rate);k=2-1.9*smooth[1];detune=smooth[2]*28;drive=1+smooth[3]*7;ringRatio=1.41421356+smooth[6]*3.72;echo=smooth[4];space=smooth[5];motion=smooth[7];}
        lfo+=2*Math.PI*(.12+motion*1.9)/Rate;if(lfo>2*Math.PI)lfo-=2*Math.PI;slowLfo+=2*Math.PI*.035/Rate;if(slowLfo>2*Math.PI)slowLfo-=2*Math.PI;double slowWave=Math.Sin(slowLfo),wave=Math.Sin(lfo),left=0,right=0,spaceBloom=0;
        if(!Stopped)foreach(Voice v in voices){if(!v.Active)continue;
          bool expanded=v.Latched||holdMode||sustain[v.Channel];if(v.Stage!=2)v.Bloom+=.0002*((expanded?bloomSmooth:0)-v.Bloom);double sustainLevel=v.Latched?Math.Max(.92,patch.Sustain):(expanded?patch.Sustain+(1-patch.Sustain)*v.Bloom*.88:patch.Sustain);
          if(v.Stage==0){v.Env+=attackStep;if(v.Env>=1){v.Env=1;v.Stage=1;}}else if(v.Stage==1)v.Env=sustainLevel+(v.Env-sustainLevel)*decayMul;else{double release=v.Bloom>.01?Math.Exp(-7/(Rate*Math.Max(patch.Release,3+11*v.Bloom))):releaseMul;v.Env*=release;if(v.Env<.00008){v.Active=false;continue;}}spaceBloom=Math.Max(spaceBloom,v.Bloom);
          v.FilterEnv*=filterMul;double drift=detune*(.55+.3*v.Drift+.15*wave);double freq=v.Freq*Math.Pow(2,(bends[v.Channel]+wave*motion*.1)/12);double d1=Math.Min(.42,freq*Math.Pow(2,-drift/1200)/Rate),d2=Math.Min(.42,freq*Math.Pow(2,drift/1200)/Rate);
          double s=.47*Osc(v.P1,d1,patch.Shape)+.43*Osc(v.P2,d2,patch.Shape)+.1*Math.Sin(v.Sub*2*Math.PI);v.P1+=d1;if(v.P1>=1)v.P1-=1;v.P2+=d2;if(v.P2>=1)v.P2-=1;v.Sub+=freq*.5/Rate;if(v.Sub>=1)v.Sub-=1;
          if(v.Bloom>.0001){double spread=14+detune*.65+slowWave*2;double d3=Math.Min(.42,freq*Math.Pow(2,-spread/1200)/Rate),d4=Math.Min(.42,freq*Math.Pow(2,spread/1200)/Rate);s=(s+v.Bloom*.24*(Osc(v.P3,d3,patch.Shape)+Osc(v.P4,d4,patch.Shape)))/(1+v.Bloom*.48);v.P3+=d3;if(v.P3>=1)v.P3-=1;v.P4+=d4;if(v.P4>=1)v.P4-=1;}
          v.Ring+=Math.Min(.45,freq*ringRatio/Rate);if(v.Ring>=1)v.Ring-=1;s*=1-smooth[6]+smooth[6]*Math.Sin(v.Ring*2*Math.PI);
          double vg=Math.Min(1.8,g*(1+patch.EnvTone*v.FilterEnv)*(1+wave*motion*.32));double a1=1/(1+vg*(vg+k));double a2=vg*a1,a3=vg*a2;double input=Sat(s*drive)*.8;double v3=input-v.Ic2;double v1=a1*v.Ic1+a2*v3;double v2=v.Ic2+a2*v.Ic1+a3*v3;v.Ic1=2*v1-v.Ic1;v.Ic2=2*v2-v.Ic2;
          double y=Sat(v2*1.3)*v.Env*v.Vel*.2;double pan=v.Drift*(.22+.28*v.Bloom)+slowWave*.13*v.Bloom;left+=y*(1-pan);right+=y*(1+pan);
        }
        chorusL[chorusPos]=left;chorusR[chorusPos]=right;double cloudL=ReadRing(chorusL,chorusPos,Rate*(.014+.004*slowWave)),cloudR=ReadRing(chorusR,chorusPos,Rate*(.022+.005*Math.Cos(slowLfo)));if(++chorusPos==chorusL.Length)chorusPos=0;double cloudMix=spaceBloom*.38;left=(left+cloudL*cloudMix)/(1+cloudMix*.55);right=(right+cloudR*cloudMix)/(1+cloudMix*.55);
        double dist=Rate*(echoSecondsSmooth+.0018*motion*wave);double dl=ReadDelay(delayL,dist),dr=ReadDelay(delayR,dist*1.013);echoDampL+=.17*(dl-echoDampL);echoDampR+=.17*(dr-echoDampR);
        delayL[delayPos]=Sat(left+echoDampR*(.18+echo*.74));delayR[delayPos]=Sat(right+echoDampL*(.18+echo*.74));if(++delayPos==delayL.Length)delayPos=0;
        double wetL=left+echoDampL*echo*.95,wetR=right+echoDampR*echo*.95,roomL=0,roomR=0;for(int j=0;j<4;j++){roomL+=reverbL[j].Tick((wetL+wetR)*.13,.72+space*.22);roomR+=reverbR[j].Tick((wetL+wetR)*.13,.72+space*.22);}
        double outL=wetL+roomL*space*.65,outR=wetR+roomR*space*.65;dcL=outL-lastL+.995*dcL;dcR=outR-lastR+.995*dcR;lastL=outL;lastR=outR;
        double a=Stopped?0:Sat(dcL*masterSmooth),b=Stopped?0:Sat(dcR*masterSmooth);if(Double.IsNaN(a)||Double.IsNaN(b)||Double.IsInfinity(a)||Double.IsInfinity(b)){InvalidSamples++;PanicCore();a=b=0;}peak=Math.Max(peak,Math.Max(Math.Abs(a),Math.Abs(b)));pcm[n*2]=(short)(a*30000);pcm[n*2+1]=(short)(b*30000);
      }Peak=(float)peak;
    }}
}

class AudioOutput : IDisposable {
    const int Frames=256,Count=3; IntPtr handle; readonly IntPtr[] headers=new IntPtr[Count],buffers=new IntPtr[Count]; readonly bool[] prepared=new bool[Count]; readonly Synth synth;
    Thread worker; volatile bool running; public volatile string Error; public long Blocks; public double WorstRenderMs;
    public AudioOutput(Synth s,uint device){synth=s;Native.Format f=new Native.Format{tag=1,channels=2,rate=Synth.Rate,bytes=Synth.Rate*4,align=4,bits=16,extra=0};uint e=Native.waveOutOpen(out handle,device,ref f,IntPtr.Zero,UIntPtr.Zero,0);if(e!=0)throw new Exception("Audio open error "+e+". Choose another output or retry.");try{for(int i=0;i<Count;i++){buffers[i]=Marshal.AllocHGlobal(Frames*4);headers[i]=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.Header)));Native.Header h=new Native.Header{data=buffers[i],length=Frames*4};Marshal.StructureToPtr(h,headers[i],false);e=Native.waveOutPrepareHeader(handle,headers[i],(uint)Marshal.SizeOf(typeof(Native.Header)));if(e!=0)throw new Exception("Audio prepare error "+e);prepared[i]=true;}running=true;worker=new Thread(Loop);worker.IsBackground=true;worker.Priority=ThreadPriority.AboveNormal;worker.Start();}catch{Dispose();throw;}}
    void Loop(){short[] data=new short[Frames*2];bool[] submitted=new bool[Count];try{while(running){bool did=false;for(int i=0;i<Count;i++){Native.Header h=(Native.Header)Marshal.PtrToStructure(headers[i],typeof(Native.Header));if(submitted[i]&&(h.flags&1)==0)continue;Stopwatch sw=Stopwatch.StartNew();synth.Render(data,Frames);sw.Stop();WorstRenderMs=Math.Max(WorstRenderMs,sw.Elapsed.TotalMilliseconds);Marshal.Copy(data,0,buffers[i],data.Length);uint e=Native.waveOutWrite(handle,headers[i],(uint)Marshal.SizeOf(typeof(Native.Header)));if(e!=0)throw new Exception("Audio stream error "+e);submitted[i]=true;Interlocked.Increment(ref Blocks);did=true;}if(!did)Thread.Sleep(1);}}catch(Exception ex){Error=ex.Message;running=false;}}
    public void Dispose(){running=false;if(worker!=null){worker.Join();worker=null;}if(handle!=IntPtr.Zero)Native.waveOutReset(handle);for(int i=0;i<Count;i++){if(headers[i]!=IntPtr.Zero){if(prepared[i]&&handle!=IntPtr.Zero)Native.waveOutUnprepareHeader(handle,headers[i],(uint)Marshal.SizeOf(typeof(Native.Header)));Marshal.FreeHGlobal(headers[i]);headers[i]=IntPtr.Zero;}if(buffers[i]!=IntPtr.Zero){Marshal.FreeHGlobal(buffers[i]);buffers[i]=IntPtr.Zero;}}if(handle!=IntPtr.Zero){Native.waveOutClose(handle);handle=IntPtr.Zero;}}
}

class MidiRouter {
    readonly Synth synth; public readonly int[] Maps=new int[8]; public int Learn=-1; public Action<int,double> Changed; public Action<string> Event;
    public MidiRouter(Synth s){synth=s;for(int i=0;i<8;i++)Maps[i]=-1;}
    public void Handle(int data){int status=data&255,ch=status&15,cmd=status&240,a=(data>>8)&127,b=(data>>16)&127;
      if(cmd==0x90){synth.NoteOn(a,b,ch);Emit(b==0?"Note off":"Note "+a+" / velocity "+b);}else if(cmd==0x80){synth.NoteOff(a,ch);Emit("Note off "+a);}else if(cmd==0xE0){synth.Bend(ch,((a+(b<<7))-8192)/8192.0*2);Emit("Pitch bend");}else if(cmd==0xB0){
        if(a==120){synth.Panic();Emit("MIDI all sound off");return;}if(a==123){synth.ReleaseChannel(ch);Emit("MIDI all notes off");return;}if(a==121){synth.ResetChannel(ch);return;}if(a==64){synth.Sustain(ch,b>=64);Emit("Sustain "+(b>=64?"on":"off"));return;}
        int key=ch*128+a;if(Learn>=0){for(int i=0;i<8;i++)if(Maps[i]==key)Maps[i]=-1;Maps[Learn]=key;Learn=-1;}
        for(int i=0;i<8;i++)if(Maps[i]==key){synth.Set(i,b/127.0);if(Changed!=null)Changed(i,b/127.0);}Emit("CC "+a+" / value "+b+" / channel "+(ch+1));
      }
    }
    void Emit(string t){if(Event!=null)Event(t);}
}

static class Theme {
    public static Color Bg=Color.FromArgb(22,26,28),Card=Color.FromArgb(33,39,41),Copper=Color.FromArgb(228,163,101),Mint=Color.FromArgb(129,213,183),Text=Color.FromArgb(236,231,218),Dim=Color.FromArgb(159,171,173);
    public static Font Font(float size,FontStyle style){return new Font("Segoe UI",size,style);}
}
class Dial : Control {
    public string Title,Hint,Mapping="unassigned"; public double Value; public bool Learning; public Action<double> Changed; public Action LearnClick; bool dragging; int startY; double startV;
    public Dial(){SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);TabStop=true;Cursor=Cursors.Hand;Size=new Size(210,168);}
    protected override void OnPaint(PaintEventArgs e){Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Theme.Card);using(Pen border=new Pen(Learning?Theme.Mint:Color.FromArgb(56,65,67),Learning?2:1))g.DrawRectangle(border,1,1,Width-3,Height-3);
      using(Font f=Theme.Font(11,FontStyle.Bold))using(Brush b=new SolidBrush(Theme.Text))g.DrawString(Title,f,b,14,12);
      Rectangle learn=new Rectangle(Width-65,10,52,24);using(Brush b=new SolidBrush(Learning?Theme.Mint:Color.FromArgb(54,65,65)))g.FillRectangle(b,learn);using(Font f=Theme.Font(8,FontStyle.Bold))using(Brush b=new SolidBrush(Learning?Theme.Bg:Theme.Mint))g.DrawString(Learning?"TURN":"LEARN",f,b,learn.X+6,learn.Y+5);
      Rectangle arc=new Rectangle(17,49,64,64);using(Pen p=new Pen(Color.FromArgb(63,72,75),6)){p.StartCap=p.EndCap=LineCap.Round;g.DrawArc(p,arc,135,270);}using(Pen p=new Pen(Theme.Copper,6)){p.StartCap=p.EndCap=LineCap.Round;g.DrawArc(p,arc,135,(float)(270*Math.Max(.002,Value)));}
      double angle=(135+270*Value)*Math.PI/180;using(Brush b=new SolidBrush(Color.FromArgb(47,54,56)))g.FillEllipse(b,26,58,46,46);using(Pen p=new Pen(Theme.Text,3))g.DrawLine(p,49,81,49+(float)(18*Math.Cos(angle)),81+(float)(18*Math.Sin(angle)));
      using(Font f=new Font("Consolas",20,FontStyle.Bold))using(Brush b=new SolidBrush(Theme.Text))g.DrawString(Math.Round(Value*100).ToString("00"),f,b,100,56);
      using(Font f=Theme.Font(8,FontStyle.Regular))using(Brush b=new SolidBrush(Theme.Dim)){g.DrawString(Hint,f,b,100,86);g.DrawString(Mapping,f,b,14,Height-23);}
      if(Focused)using(Pen p=new Pen(Theme.Mint))g.DrawRectangle(p,5,5,Width-11,Height-11);
    }
    public void SetValue(double v,bool notify){Value=Math.Max(0,Math.Min(1,v));Invalidate();if(notify&&Changed!=null)Changed(Value);}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(e.Button!=MouseButtons.Left)return;if(e.X>Width-70&&e.Y<39){if(LearnClick!=null)LearnClick();return;}dragging=true;startY=e.Y;startV=Value;Capture=true;}
    protected override void OnMouseMove(MouseEventArgs e){if(dragging)SetValue(startV+(startY-e.Y)/140.0,true);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;}
    protected override void OnMouseWheel(MouseEventArgs e){SetValue(Value+e.Delta/120*.025,true);}
    protected override bool IsInputKey(Keys keyData){if(keyData==Keys.Left||keyData==Keys.Right||keyData==Keys.Up||keyData==Keys.Down)return true;return base.IsInputKey(keyData);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Right)SetValue(Value+.02,true);if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Left)SetValue(Value-.02,true);base.OnKeyDown(e);}
}
class Choice : ComboBox {
    public Choice(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Card);using(Pen p=new Pen(Focused?Theme.Mint:Color.FromArgb(60,72,74)))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);using(Brush b=new SolidBrush(Theme.Text)){e.Graphics.DrawString(Text,Font,b,new RectangleF(8,5,Width-34,Height-5));Point[] triangle={new Point(Width-20,Height/2-2),new Point(Width-10,Height/2-2),new Point(Width-15,Height/2+3)};e.Graphics.FillPolygon(b,triangle);}}
    protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
}
class Piano : Control {
    readonly Synth synth; public int BaseNote=48; int held=-1; public Action<int> Press,Release;
    public Piano(Synth s){synth=s;DoubleBuffered=true;Height=128;TabStop=false;Cursor=Cursors.Hand;}
    static bool Black(int n){int x=n%12;return x==1||x==3||x==6||x==8||x==10;}
    List<KeyValuePair<int,Rectangle>> KeysOf(bool black){List<KeyValuePair<int,Rectangle>> list=new List<KeyValuePair<int,Rectangle>>();int whites=0;for(int i=0;i<25;i++)if(!Black(BaseNote+i))whites++;double w=Width/(double)whites;int wi=0;for(int i=0;i<25;i++){int note=BaseNote+i;if(Black(note)){if(black)list.Add(new KeyValuePair<int,Rectangle>(note,new Rectangle((int)(wi*w-w*.32),0,(int)(w*.64),Height*62/100)));}else{if(!black)list.Add(new KeyValuePair<int,Rectangle>(note,new Rectangle((int)(wi*w),0,(int)((wi+1)*w)-(int)(wi*w)-1,Height-1)));wi++;}}return list;}
    protected override void OnPaint(PaintEventArgs e){foreach(bool black in new bool[]{false,true})foreach(KeyValuePair<int,Rectangle> k in KeysOf(black)){bool on=synth.IsNote(k.Key);using(Brush b=new SolidBrush(on?Theme.Mint:(black?Color.FromArgb(26,31,33):Theme.Text)))e.Graphics.FillRectangle(b,k.Value);using(Pen p=new Pen(Theme.Bg))e.Graphics.DrawRectangle(p,k.Value);if(!black&&k.Key%12==0)using(Font f=Theme.Font(8,FontStyle.Regular))using(Brush b=new SolidBrush(Theme.Bg))e.Graphics.DrawString("C"+(k.Key/12-1),f,b,k.Value.X+5,Height-24);}}
    int Hit(Point p){foreach(bool black in new bool[]{true,false})foreach(KeyValuePair<int,Rectangle> k in KeysOf(black))if(k.Value.Contains(p))return k.Key;return -1;}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){held=Hit(e.Location);Capture=true;if(held>=0&&Press!=null)Press(held);}}
    protected override void OnMouseMove(MouseEventArgs e){if(Capture&&e.Button==MouseButtons.Left){int next=Hit(e.Location);if(next!=held){if(held>=0&&Release!=null)Release(held);held=next;if(held>=0&&Press!=null)Press(held);}}}
    protected override void OnMouseUp(MouseEventArgs e){EndNote();}
    protected override void OnMouseCaptureChanged(EventArgs e){if(!Capture)EndNote();base.OnMouseCaptureChanged(e);}
    void EndNote(){int old=held;held=-1;Capture=false;if(old>=0&&Release!=null)Release(old);}
}

class MainForm : Form {
    public readonly Synth Engine=new Synth(); public readonly MidiRouter Router; readonly MidiInput midi=new MidiInput(); AudioOutput audio;
    readonly Dial[] dials=new Dial[8]; readonly ComboBox presets=new Choice(),midiChoice=new Choice(),audioChoice=new Choice();
    readonly Label description=new Label(),midiStatus=new Label(),audioStatus=new Label(),activity=new Label(),footer=new Label();
    readonly Button stop=new Button(); readonly TrackBar volume=new TrackBar(); readonly ProgressBar meter=new ProgressBar(); readonly Piano piano;
    readonly CheckBox holdToggle=new CheckBox(),droneToggle=new CheckBox(); bool updatingHold;
    readonly TrackBar bloomControl=new TrackBar(),echoTimeControl=new TrackBar(); readonly Label echoTimeValue=new Label();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); readonly Dictionary<Keys,int> heldKeys=new Dictionary<Keys,int>();
    readonly string settings=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"knob-mappings.txt"); string[] midiNames=new string[0]; int ticks,octave=4; bool updating,testing; string audioFailure; public string MidiState {get{return midiStatus.Text;}} public string AudioState {get{return audioStatus.Text;}}
    static readonly Keys[] noteKeys={Keys.A,Keys.W,Keys.S,Keys.E,Keys.D,Keys.F,Keys.T,Keys.G,Keys.Y,Keys.H,Keys.U,Keys.J,Keys.K,Keys.O,Keys.L,Keys.P,Keys.OemSemicolon};
    static readonly string[] titles={"Tone","Squelch","Drift","Grit","Tape echo","Room","Metal","Motion"};
    static readonly string[] hints={"dark > open","filter resonance","detune + wander","soft saturation","long repeats","larger reverb","ring modulation","slow > restless"};
    public MainForm(bool test){testing=test;Router=new MidiRouter(Engine);Text="TCKO2 Synth modular · experimental synthesizer";BackColor=Theme.Bg;ForeColor=Theme.Text;Font=Theme.Font(10,FontStyle.Regular);ClientSize=new Size(1000,740);MinimumSize=new Size(940,779);StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;AutoScaleMode=AutoScaleMode.Dpi;
      TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=8,BackColor=Theme.Bg};root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));root.RowStyles.Add(new RowStyle(SizeType.Absolute,73));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,40));root.RowStyles.Add(new RowStyle(SizeType.Absolute,96));root.RowStyles.Add(new RowStyle(SizeType.Absolute,31));Controls.Add(root);
      Panel heading=new Panel{Dock=DockStyle.Fill,Width=952};Label logo=LabelAt("TCKO2 Synth modular",0,0,410,34,22,Theme.Text);logo.Font=Theme.Font(22,FontStyle.Bold);heading.Controls.Add(logo);heading.Controls.Add(LabelAt("A small laboratory for unstable sound   /   PROTOTYPE 03",2,34,610,17,8,Theme.Copper));root.Controls.Add(heading,0,0);
      TableLayoutPanel connect=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};connect.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));connect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));connect.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,124));connect.Controls.Add(new Label{Text="USB / MIDI",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Mint},0,0);StyleCombo(midiChoice);connect.Controls.Add(midiChoice,1,0);Button scan=MakeButton("Rescan MIDI",Theme.Card);scan.Click+=delegate{RefreshMidi(true);};connect.Controls.Add(scan,2,0);
      connect.Controls.Add(new Label{Text="PC / AUDIO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Copper},0,1);StyleCombo(audioChoice);audioChoice.Items.AddRange(Native.AudioNames());audioChoice.SelectedIndex=0;connect.Controls.Add(audioChoice,1,1);Button retry=MakeButton("Restart audio",Theme.Card);retry.Click+=delegate{RefreshAudio();};connect.Controls.Add(retry,2,1);root.Controls.Add(connect,0,1);
      Panel patchRow=new Panel{Dock=DockStyle.Fill,Width=952};StyleCombo(presets);presets.Dock=DockStyle.None;presets.SetBounds(0,7,320,33);foreach(Patch p in Patch.All)presets.Items.Add(p.Name);patchRow.Controls.Add(presets);description.SetBounds(0,48,900,26);description.ForeColor=Theme.Dim;patchRow.Controls.Add(description);stop.Text="STOP / RESUME";StyleButton(stop,Theme.Card);stop.SetBounds(655,5,150,36);stop.Anchor=AnchorStyles.Top|AnchorStyles.Right;stop.Click+=delegate{ToggleStop();};patchRow.Controls.Add(stop);Button panic=MakeButton("PANIC  /  Esc",Color.FromArgb(108,52,43));panic.Dock=DockStyle.None;panic.SetBounds(817,5,134,36);panic.Anchor=AnchorStyles.Top|AnchorStyles.Right;panic.Click+=delegate{Panic();};patchRow.Controls.Add(panic);root.Controls.Add(patchRow,0,2);
      Button soundSettings=MakeButton("Sound settings",Theme.Card);soundSettings.Dock=DockStyle.None;soundSettings.SetBounds(477,5,150,36);soundSettings.Anchor=AnchorStyles.Top|AnchorStyles.Right;soundSettings.Click+=delegate{try{Process.Start("ms-settings:sound");}catch(Exception ex){activity.Text=ex.Message;}};patchRow.Controls.Add(soundSettings);
      Panel holds=new Panel{Dock=DockStyle.Fill};holdToggle.Text="SUSTAIN LATCH";holdToggle.SetBounds(0,2,175,26);holdToggle.ForeColor=Theme.Mint;holdToggle.FlatStyle=FlatStyle.Flat;holdToggle.Font=Theme.Font(9,FontStyle.Bold);droneToggle.Text="DRONE LATCH";droneToggle.SetBounds(182,2,164,26);droneToggle.ForeColor=Theme.Copper;droneToggle.FlatStyle=FlatStyle.Flat;droneToggle.Font=Theme.Font(9,FontStyle.Bold);holds.Controls.Add(holdToggle);holds.Controls.Add(droneToggle);holdToggle.CheckedChanged+=delegate{if(updatingHold)return;Engine.SetHold(holdToggle.Checked);UpdateHold();};droneToggle.CheckedChanged+=delegate{if(updatingHold)return;Engine.SetDrone(droneToggle.Checked);UpdateHold();};
      holds.Controls.Add(LabelAt("BLOOM",350,7,64,22,8,Theme.Mint));bloomControl.SetBounds(408,0,126,32);bloomControl.Minimum=0;bloomControl.Maximum=100;bloomControl.Value=75;bloomControl.TickStyle=TickStyle.None;bloomControl.ValueChanged+=delegate{Engine.SetBloom(bloomControl.Value/100.0);};holds.Controls.Add(bloomControl);
      holds.Controls.Add(LabelAt("ECHO TIME",553,7,92,22,8,Theme.Copper));echoTimeControl.SetBounds(643,0,220,32);echoTimeControl.Minimum=80;echoTimeControl.Maximum=4000;echoTimeControl.SmallChange=20;echoTimeControl.LargeChange=200;echoTimeControl.Value=850;echoTimeControl.TickStyle=TickStyle.None;echoTimeValue.SetBounds(870,7,80,22);echoTimeValue.ForeColor=Theme.Text;echoTimeValue.Font=new Font("Consolas",10);echoTimeControl.ValueChanged+=delegate{Engine.SetEchoTime(echoTimeControl.Value/1000.0);echoTimeValue.Text=(echoTimeControl.Value/1000.0).ToString("0.00")+" s";};holds.Controls.Add(echoTimeControl);holds.Controls.Add(echoTimeValue);root.Controls.Add(holds,0,3);
      TableLayoutPanel knobs=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2};for(int i=0;i<4;i++)knobs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));knobs.RowStyles.Add(new RowStyle(SizeType.Percent,50));knobs.RowStyles.Add(new RowStyle(SizeType.Percent,50));for(int i=0;i<8;i++){int index=i;Dial d=new Dial{Title=titles[i],Hint=hints[i],Dock=DockStyle.Fill,Margin=new Padding(0,0,10,10)};d.Changed=delegate(double v){Engine.Set(index,v);};d.LearnClick=delegate{Router.Learn=Router.Learn==index?-1:index;UpdateMaps();activity.Text=Router.Learn>=0?"Learning "+titles[index]+": turn an Akai knob (CC).":"MIDI learn cancelled.";};dials[i]=d;knobs.Controls.Add(d,i%4,i/4);}root.Controls.Add(knobs,0,4);
      Panel level=new Panel{Dock=DockStyle.Fill,Width=952};level.Controls.Add(LabelAt("MASTER",0,7,78,24,9,Theme.Mint));volume.SetBounds(76,0,270,42);volume.Minimum=0;volume.Maximum=80;volume.Value=20;volume.TickStyle=TickStyle.None;volume.SmallChange=1;volume.LargeChange=5;volume.Scroll+=delegate{Engine.Volume(volume.Value/100.0);};level.Controls.Add(volume);level.Controls.Add(LabelAt("Start low · PC speakers / headphones",355,7,370,25,9,Theme.Dim));meter.SetBounds(779,10,172,16);meter.Maximum=100;meter.Anchor=AnchorStyles.Right|AnchorStyles.Top;level.Controls.Add(meter);root.Controls.Add(level,0,5);
      piano=new Piano(Engine){Dock=DockStyle.Fill};piano.Press=delegate(int n){Engine.NoteOn(n,100,17);};piano.Release=delegate(int n){Engine.NoteOff(n,17);};root.Controls.Add(piano,0,6);footer.Text="Keys: A W S E D F T G Y H U J K O L P ;    ·    Z / X octave    ·    Esc panic    ·    drag dials / wheel";footer.Dock=DockStyle.Fill;footer.ForeColor=Theme.Dim;footer.Font=Theme.Font(9,FontStyle.Regular);footer.TextAlign=ContentAlignment.MiddleLeft;root.Controls.Add(footer,0,7);
      // Connection health is kept visible in the header and an activity line beneath it.
      midiStatus=LabelAt("",620,0,330,20,9,Theme.Mint);midiStatus.Anchor=AnchorStyles.Top|AnchorStyles.Right;heading.Controls.Add(midiStatus);audioStatus=LabelAt("",620,19,330,20,9,Theme.Copper);audioStatus.Anchor=AnchorStyles.Top|AnchorStyles.Right;heading.Controls.Add(audioStatus);activity=LabelAt("Waiting for a note or a knob…",620,38,330,18,8,Theme.Dim);activity.Anchor=AnchorStyles.Top|AnchorStyles.Right;heading.Controls.Add(activity);
      Router.Changed=delegate(int i,double v){dials[i].SetValue(v,false);};Router.Event=delegate(string text){activity.Text=text;UpdateMaps();UpdateHold();};LoadMaps();
      presets.SelectedIndexChanged+=delegate{Engine.SetPatch(presets.SelectedIndex);heldKeys.Clear();UpdateHold();bloomControl.Value=(int)Math.Round(Engine.BloomAmount*100);echoTimeControl.Value=(int)Math.Round(Engine.EchoTime*1000);echoTimeValue.Text=Engine.EchoTime.ToString("0.00")+" s";description.Text=Patch.All[presets.SelectedIndex].Description;for(int i=0;i<8;i++)dials[i].SetValue(Engine.Get(i),false);};presets.SelectedIndex=0;
      midiChoice.SelectedIndexChanged+=delegate{if(!updating)ConnectMidi();};audioChoice.SelectedIndexChanged+=delegate{if(!testing&&!updating)StartAudio();};
      KeyDown+=KeyPressed;KeyUp+=KeyReleased;Deactivate+=delegate{ReleaseComputer();Engine.ReleaseKeys(17);};FormClosing+=delegate{timer.Stop();Panic();SaveMaps();midi.Dispose();if(audio!=null)audio.Dispose();};
      timer.Interval=15;timer.Tick+=Tick;Shown+=delegate{RefreshMidi(true);if(!testing)StartAudio();timer.Start();};
    }
    Label LabelAt(string t,int x,int y,int w,int h,float size,Color color){return new Label{Text=t,Bounds=new Rectangle(x,y,w,h),Font=Theme.Font(size,FontStyle.Regular),ForeColor=color};}
    static void StyleCombo(ComboBox c){c.DropDownStyle=ComboBoxStyle.DropDownList;c.FlatStyle=FlatStyle.Flat;c.BackColor=Theme.Card;c.ForeColor=Theme.Text;c.Font=Theme.Font(11,FontStyle.Regular);c.Dock=DockStyle.Fill;c.Margin=new Padding(0,4,12,4);c.DrawMode=DrawMode.OwnerDrawFixed;c.ItemHeight=24;c.DrawItem+=delegate(object sender,DrawItemEventArgs e){using(Brush bg=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(55,68,66):Theme.Card))e.Graphics.FillRectangle(bg,e.Bounds);if(e.Index>=0)using(Brush fg=new SolidBrush(Theme.Text))e.Graphics.DrawString(c.Items[e.Index].ToString(),c.Font,fg,e.Bounds.X+7,e.Bounds.Y+3);};}
    static void StyleButton(Button b,Color color){b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=color;b.ForeColor=Theme.Text;b.Font=Theme.Font(9,FontStyle.Bold);b.Cursor=Cursors.Hand;}
    static Button MakeButton(string text,Color color){Button b=new Button{Text=text,Dock=DockStyle.Fill,Margin=new Padding(0,4,0,4)};StyleButton(b,color);return b;}
    void StartAudio(){Panic();audioFailure=null;if(audio!=null){audio.Dispose();audio=null;}try{uint device=audioChoice.SelectedIndex<=0?uint.MaxValue:(uint)(audioChoice.SelectedIndex-1);audio=new AudioOutput(Engine,device);audioStatus.Text="Audio ready · 44.1 kHz stereo";}catch(Exception ex){audioFailure=ex.Message;audioStatus.Text="AUDIO OFFLINE · check Sound settings";activity.Text=ex.Message;}}
    void RefreshAudio(){string previous=audioChoice.Text;updating=true;audioChoice.Items.Clear();audioChoice.Items.AddRange(Native.AudioNames());int index=audioChoice.Items.IndexOf(previous);audioChoice.SelectedIndex=index>=0?index:0;updating=false;StartAudio();}
    void RefreshMidi(bool force){string[] names=Native.MidiNames();if(!force&&String.Join("|",names)==String.Join("|",midiNames))return;string previous=midi.Name;midi.Dispose();Engine.Panic();midiNames=names;updating=true;midiChoice.Items.Clear();midiChoice.Items.Add("Computer / on-screen keyboard only");midiChoice.Items.AddRange(names);int selected=0;for(int i=0;i<names.Length;i++)if(names[i]==previous)selected=i+1;if(selected==0)for(int i=0;i<names.Length;i++)if(names[i].ToLowerInvariant().Contains("mpk")||names[i].ToLowerInvariant().Contains("akai")){selected=i+1;break;}midiChoice.SelectedIndex=selected;updating=false;ConnectMidi();}
    void ConnectMidi(){midi.Dispose();Engine.Panic();if(midiChoice.SelectedIndex<=0){midiStatus.Text=midiNames.Length==0?"No USB MIDI detected · fallback ready":"MIDI disconnected · fallback ready";return;}try{int i=midiChoice.SelectedIndex-1;midi.Open(i,midiNames[i]);midiStatus.Text="CONNECTED · "+midi.Name;}catch(Exception ex){midiStatus.Text="MIDI OFFLINE · fallback ready";activity.Text=ex.Message;}}
    void Tick(object sender,EventArgs args){int data,n=0;if(midi.Overflow){while(midi.Pop(out data)){}Panic();midi.Overflow=false;activity.Text="MIDI overload cleared.";}while(n++<512&&midi.Pop(out data))Router.Handle(data);if(Router.Learn<0&&mapDirty){SaveMaps();mapDirty=false;}UpdateHold();piano.Invalidate();meter.Value=Math.Min(100,(int)(Engine.Peak*100));if(audio!=null&&audio.Error!=null){audioStatus.Text="AUDIO OFFLINE";activity.Text=audio.Error;}if(++ticks%200==0)RefreshMidi(false);}
    bool mapDirty;
    void UpdateMaps(){for(int i=0;i<8;i++){dials[i].Learning=Router.Learn==i;int m=Router.Maps[i];string next=m<0?"unassigned · click Learn":"CC "+(m%128)+" · channel "+(m/128+1);if(dials[i].Mapping!=next){mapDirty=true;dials[i].Mapping=next;}dials[i].Invalidate();}}
    void LoadMaps(){try{if(File.Exists(settings)){string[] lines=File.ReadAllLines(settings);for(int i=0;i<8&&i<lines.Length;i++){int value;if(int.TryParse(lines[i],out value)&&value>=-1&&value<2048)Router.Maps[i]=value;}}}catch{activity.Text="Could not load knob mappings.";}UpdateMaps();mapDirty=false;}
    void SaveMaps(){if(testing)return;try{string[] lines=new string[8];for(int i=0;i<8;i++)lines[i]=Router.Maps[i].ToString();File.WriteAllLines(settings,lines);}catch{activity.Text="Mappings work now; unable to save in this folder.";}}
    void UpdateHold(){updatingHold=true;holdToggle.Checked=Engine.HoldMode;droneToggle.Checked=Engine.DroneMode;updatingHold=false;}
    public void Panic(){Engine.Panic();heldKeys.Clear();Router.Learn=-1;UpdateMaps();UpdateHold();activity.Text="Panic: notes, latches and effect tails cleared.";}
    void ToggleStop(){Engine.Stopped=!Engine.Stopped;Panic();stop.Text=Engine.Stopped?"RESUME SOUND":"STOP / RESUME";stop.BackColor=Engine.Stopped?Color.FromArgb(108,52,43):Theme.Card;activity.Text=Engine.Stopped?"Stopped · click Resume to play again.":"Sound resumed.";}
    void ReleaseComputer(){foreach(int note in heldKeys.Values)Engine.NoteOff(note,16);heldKeys.Clear();}
    void KeyPressed(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){Panic();e.SuppressKeyPress=true;return;}if((e.KeyCode==Keys.Z||e.KeyCode==Keys.X)&&!heldKeys.ContainsKey(e.KeyCode)){ReleaseComputer();octave=Math.Max(1,Math.Min(7,octave+(e.KeyCode==Keys.Z?-1:1)));piano.BaseNote=octave*12;piano.Invalidate();e.SuppressKeyPress=true;return;}if(e.Control||e.Alt||presets.DroppedDown||midiChoice.DroppedDown||audioChoice.DroppedDown)return;for(int i=0;i<noteKeys.Length;i++)if(e.KeyCode==noteKeys[i]){if(!heldKeys.ContainsKey(e.KeyCode)){int note=octave*12+i;heldKeys[e.KeyCode]=note;Engine.NoteOn(note,100,16);}e.SuppressKeyPress=true;return;}}
    void KeyReleased(object sender,KeyEventArgs e){int note;if(heldKeys.TryGetValue(e.KeyCode,out note)){Engine.NoteOff(note,16);heldKeys.Remove(e.KeyCode);e.SuppressKeyPress=true;}}
    public void TestUI(string path){
      presets.SelectedIndex=3;dials[6].SetValue(.63,true);Router.Learn=0;Router.Handle(0xB0|(74<<8)|(95<<16));if(Math.Abs(dials[0].Value-95/127.0)>.001)throw new Exception("UI MIDI learn failed");
      bloomControl.Value=100;echoTimeControl.Value=4000;if(Engine.BloomAmount!=1||Engine.EchoTime!=4)throw new Exception("UI Bloom / Echo Time control failed");
      KeyPressed(this,new KeyEventArgs(Keys.A));if(!Engine.IsNote(48))throw new Exception("Computer key failed");KeyReleased(this,new KeyEventArgs(Keys.A));if(Engine.IsNote(48))throw new Exception("Computer key release failed");
      holdToggle.Checked=true;KeyPressed(this,new KeyEventArgs(Keys.A));KeyReleased(this,new KeyEventArgs(Keys.A));if(!Engine.IsNote(48))throw new Exception("UI sustain latch failed");holdToggle.Checked=false;if(Engine.IsNote(48))throw new Exception("UI sustain unlatch failed");
      droneToggle.Checked=true;KeyPressed(this,new KeyEventArgs(Keys.A));KeyReleased(this,new KeyEventArgs(Keys.A));if(!Engine.IsNote(48))throw new Exception("UI drone latch failed");KeyPressed(this,new KeyEventArgs(Keys.A));KeyReleased(this,new KeyEventArgs(Keys.A));if(Engine.IsNote(48))throw new Exception("UI drone note toggle failed");
      presets.SelectedIndex=5;if(!droneToggle.Checked||holdToggle.Checked)throw new Exception("Drone preset should enable drone mode");holdToggle.Checked=true;if(droneToggle.Checked)throw new Exception("Latch mode switching failed");ToggleStop();if(!Engine.Stopped||holdToggle.Checked||droneToggle.Checked)throw new Exception("Stop should clear latches");ToggleStop();if(Engine.Stopped)throw new Exception("Resume failed");droneToggle.Checked=true;Panic();if(holdToggle.Checked||droneToggle.Checked)throw new Exception("Panic should clear latch controls");
      for(int i=0;i<8;i++)Router.Maps[i]=-1;UpdateMaps();StartAudio();presets.SelectedIndex=6;using(Bitmap b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    }
    public long AudioBlocks{get{return audio==null?0:Interlocked.Read(ref audio.Blocks);}}
    public string AudioError{get{return audio==null?(audioFailure??"No audio output"):audio.Error;}}
}

static class Tests {
    static void Check(bool b,string name,StringBuilder report){if(!b)throw new Exception("FAILED: "+name);report.AppendLine("PASS: "+name);}
    static int Msg(int status,int a,int b){return status|(a<<8)|(b<<16);}
    public static void Run(string dir,bool coreOnly){string reportFile=Path.Combine(dir,coreOnly?"verification-core.txt":"verification.txt");StringBuilder r=new StringBuilder();r.AppendLine("TCKO2 Synth modular prototype 03 verification · "+DateTime.Now.ToString("s"));r.AppendLine("MIDI inputs: "+String.Join(", ",Native.MidiNames()));r.AppendLine("Audio outputs: "+String.Join(", ",Native.AudioNames()));try{
      Check(Marshal.SizeOf(typeof(Native.Header))==(IntPtr.Size==8?48:32),"Native WAVEHDR layout",r);Synth s=new Synth();MidiRouter router=new MidiRouter(s);short[] block=new short[512];
      router.Handle(Msg(0x90,60,100));Check(s.IsNote(60),"MIDI note-on",r);router.Handle(Msg(0x90,60,0));Check(!s.IsNote(60),"Velocity-zero note-off",r);
      router.Handle(Msg(0x91,62,100));router.Handle(Msg(0xB1,64,127));router.Handle(Msg(0x81,62,0));Check(s.IsNote(62),"Channel sustain holds note",r);router.Handle(Msg(0xB1,64,0));Check(!s.IsNote(62),"Sustain releases note",r);
      s.Panic();s.SetHold(true);router.Handle(Msg(0x90,60,100));router.Handle(Msg(0x80,60,0));router.Handle(Msg(0x91,64,100));router.Handle(Msg(0x81,64,0));Check(s.IsNote(60)&&s.IsNote(64),"Sustain latch holds across MIDI channels",r);router.Handle(Msg(0xB1,64,0));Check(s.IsNote(64),"Pedal-off preserves software sustain",r);router.Handle(Msg(0xB0,64,127));s.SetHold(false);Check(s.IsNote(60)&&!s.IsNote(64),"Unlatch respects a still-held MIDI pedal",r);router.Handle(Msg(0xB0,64,0));Check(!s.IsNote(60),"Final pedal-off releases held note",r);
      s.Panic();s.SetPatch(3);s.SetDrone(true);router.Handle(Msg(0x92,57,100));router.Handle(Msg(0x92,57,0));Check(s.IsNote(57),"Drone latches after velocity-zero note-off",r);for(int i=0;i<700;i++)s.Render(block,256);Check(s.IsNote(57)&&s.Peak>.001&&s.InvalidSamples==0,"Drone produces sustained audio after normal release time",r);router.Handle(Msg(0x92,57,100));router.Handle(Msg(0x82,57,0));Check(!s.IsNote(57),"Second drone tap releases without relatching",r);
      s.NoteOn(57,100,2);s.NoteOff(57,2);s.NoteOn(64,100,17);s.NoteOff(64,17);s.ReleaseKeys(17);Check(s.IsNote(64),"Focus loss preserves intentional drone latch",r);s.SetDrone(false);Check(!s.IsNote(57)&&!s.IsNote(64),"Disabling drone releases latched chord",r);
      s.SetDrone(true);s.NoteOn(60,100,0);s.NoteOff(60,0);router.Handle(Msg(0xB0,123,0));Check(!s.IsNote(60),"MIDI all-notes-off clears drone voice",r);s.Panic();Check(!s.HoldMode&&!s.DroneMode,"Panic resets both latch modes",r);s.SetPatch(5);Check(s.DroneMode&&!s.HoldMode,"Dedicated drone preset enables latch",r);s.SetPatch(0);Check(!s.DroneMode&&!s.HoldMode,"Normal preset clears latch modes",r);
      router.Learn=2;router.Handle(Msg(0xB2,71,99));Check(router.Maps[2]==327&&Math.Abs(s.Get(2)-99/127.0)<.0001,"CC learn with channel and value",r);router.Handle(Msg(0xB0,71,10));Check(Math.Abs(s.Get(2)-99/127.0)<.0001,"CC channel isolation",r);
      router.Learn=4;router.Handle(Msg(0xB2,71,20));Check(router.Maps[2]==-1&&router.Maps[4]==327,"Reassignment clears previous mapping",r);
      router.Handle(Msg(0x92,64,127));router.Handle(Msg(0xB2,123,0));Check(!s.IsNote(64),"All notes off",r);s.Panic();s.Render(block,256);bool silent=true;foreach(short v in block)if(v!=0)silent=false;Check(silent,"Panic clears audio and effect tails",r);
      long total=0;double peak=0;Stopwatch sw=Stopwatch.StartNew();using(BinaryWriter w=new BinaryWriter(File.Create(Path.Combine(dir,"preset-preview.wav")))){int seconds=Patch.All.Length*8,bytes=seconds*Synth.Rate*4;w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(bytes+36);w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((ushort)1);w.Write((ushort)2);w.Write(Synth.Rate);w.Write(Synth.Rate*4);w.Write((ushort)4);w.Write((ushort)16);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(bytes);
        for(int p=0;p<Patch.All.Length;p++){s.SetPatch(p);s.Volume(.28);double energy=0;int frames=8*Synth.Rate;short[] data=new short[512];for(int frame=0;frame<frames;frame+=256){if(frame==0){s.NoteOn(p==1?36:48,95,0);if(p!=1){s.NoteOn(55,78,0);s.NoteOn(60,85,0);}}if(frame>=Synth.Rate*4&&frame<Synth.Rate*4+256){s.NoteOff(p==1?36:48,0);s.NoteOff(55,0);s.NoteOff(60,0);}int count=Math.Min(256,frames-frame);s.Render(data,count);for(int j=0;j<count*2;j++){w.Write(data[j]);double v=data[j]/32768.0;energy+=v*v;peak=Math.Max(peak,Math.Abs(v));}total+=count;}Check(energy>1,"Non-silent bounded audio: "+Patch.All[p].Name,r);r.AppendLine("  RMS "+Math.Sqrt(energy/(frames*2)).ToString("F4"));}}
      sw.Stop();r.AppendLine("Offline generation: "+total+" stereo frames in "+sw.Elapsed.TotalSeconds.ToString("F2")+"s; peak "+peak.ToString("F4"));Check(peak<.92,"Output headroom",r);
      s.Panic();s.Stopped=true;s.NoteOn(60,100,0);s.Render(block,256);silent=true;foreach(short v in block)if(v!=0)silent=false;Check(silent&&!s.IsNote(60),"Stopped rejects notes and renders silence",r);s.Stopped=false;
      for(int p=0;p<Patch.All.Length;p++){s.SetPatch(p);for(int i=0;i<8;i++)s.Set(i,1);for(int n=0;n<32;n++)s.NoteOn(24+n*3,127,n%16);for(int n=0;n<350;n++){s.Render(block,256);foreach(short v in block)CheckRange(v);}Check(s.ActiveCount<=8&&s.InvalidSamples==0,"Eight-voice limit + finite audio at maximum controls: "+p,r);}s.Panic();
      VerifyExpanse(r);VerifyLongEcho(r);
      using(MainForm form=new MainForm(true)){form.Show();Application.DoEvents();form.TestUI(Path.Combine(dir,"TCKO2Synth-preview.png"));Check(true,"UI presets, Bloom, Echo Time, dials, learn, keyboard, sustain, drone, stop/resume and panic",r);long before=form.AudioBlocks;Stopwatch wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<700){Application.DoEvents();Thread.Sleep(10);}r.AppendLine(form.MidiState);r.AppendLine(form.AudioState);r.AppendLine("Audio blocks: "+form.AudioBlocks+"; error: "+(form.AudioError??"none"));if(coreOnly)r.AppendLine("SKIP: Live Windows audio assertion (--core-test). Native output result above remains unverified or unavailable.");else Check(form.AudioError==null&&form.AudioBlocks>before+10,"Native Windows audio buffers complete and recycle (silent)",r);form.Close();}
      r.AppendLine("Physical key / knob events and audible PC output require a human play check.");r.AppendLine(coreOnly?"RESULT: PASS (DSP/MIDI/UI core checks only; live audio excluded)":"RESULT: PASS");
    }catch(Exception ex){r.AppendLine(ex.ToString());r.AppendLine("RESULT: FAIL");File.WriteAllText(reportFile,r.ToString());throw;}File.WriteAllText(reportFile,r.ToString());}
    static void CheckRange(short v){if(Math.Abs((int)v)>30000)throw new Exception("Audio out of range");}
    static double StereoCorrelation(Synth s){short[] pcm=new short[512];double ll=0,rr=0,lr=0;for(int i=0;i<690;i++){s.Render(pcm,256);if(i<345)continue;for(int j=0;j<512;j+=2){double l=pcm[j],r=pcm[j+1];ll+=l*l;rr+=r*r;lr+=l*r;}}return lr/Math.Sqrt(ll*rr);}
    static Synth BloomTestSynth(double bloom){Synth s=new Synth();s.SetPatch(3);for(int i=0;i<8;i++)s.Set(i,0);s.Set(0,.7);s.SetBloom(bloom);s.SetHold(true);s.NoteOn(57,100,0);s.NoteOff(57,0);return s;}
    static void VerifyExpanse(StringBuilder r){Synth dry=BloomTestSynth(0),wide=BloomTestSynth(1);double dryCorrelation=StereoCorrelation(dry),wideCorrelation=StereoCorrelation(wide);r.AppendLine("Stereo correlation: dry "+dryCorrelation.ToString("F4")+", Bloom "+wideCorrelation.ToString("F4"));Check(wideCorrelation<dryCorrelation-.01,"Bloom adds stereo spread to sustained notes",r);wide.SetHold(false);short[] pcm=new short[512];for(int i=0;i<520;i++)wide.Render(pcm,256);Check(wide.ActiveCount>0&&wide.Peak>.001,"Bloom gives held notes a long natural release",r);wide.Panic();wide.Render(pcm,256);bool silent=true;foreach(short v in pcm)if(v!=0)silent=false;Check(silent,"Panic clears expanded chorus and long effect tails",r);
      Synth maximum=new Synth();maximum.SetPatch(7);maximum.SetBloom(1);maximum.SetEchoTime(4);maximum.Volume(.8);for(int i=0;i<8;i++){maximum.Set(i,1);maximum.NoteOn(48+i,127,0);}Stopwatch sw=Stopwatch.StartNew();for(int i=0;i<3500;i++){maximum.Render(pcm,256);foreach(short v in pcm)CheckRange(v);}sw.Stop();r.AppendLine("Maximum 8-voice Bloom + 4s echo: 20.3s rendered in "+sw.Elapsed.TotalSeconds.ToString("F2")+"s.");Check(maximum.InvalidSamples==0&&maximum.ActiveCount==8,"Long feedback stability at maximum Bloom, echo and reverb",r);Check(sw.Elapsed.TotalSeconds<20,"Expanded engine renders faster than real time",r);
    }
    static void VerifyLongEcho(StringBuilder r){Synth s=new Synth();s.SetPatch(1);for(int i=0;i<8;i++)s.Set(i,0);s.Set(0,.7);s.Set(4,1);s.SetBloom(0);s.SetEchoTime(1.25);short[] pcm=new short[512];for(int i=0;i<350;i++)s.Render(pcm,256);s.NoteOn(69,100,0);double early=0,first=0,late=0;for(int frame=0;frame<5*Synth.Rate;frame+=256){if(frame>=Synth.Rate*.1&&frame<Synth.Rate*.1+256)s.NoteOff(69,0);s.Render(pcm,256);double t=frame/(double)Synth.Rate;double energy=0;foreach(short v in pcm){double x=v/32768.0;energy+=x*x;}if(t>.7&&t<.95)early+=energy;if(t>1.22&&t<1.55)first+=energy;if(t>3.72&&t<3.99)late+=energy;}r.AppendLine("Echo window energy: before repeat "+early.ToString("F6")+", first "+first.ToString("F6")+", third "+late.ToString("F6"));Check(first>.01&&first>early*10,"Echo Time delays the first repeat beyond one second",r);Check(late>.001&&s.InvalidSamples==0,"Echo repeats persist several seconds after a short note",r);s.SetEchoTime(99);Check(s.EchoTime==4,"Echo Time supports four seconds and clamps upper range",r);s.SetEchoTime(-1);Check(s.EchoTime==.08,"Echo Time retains a short slapback setting",r);}
}
static class Program {
    [STAThread] static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{if(args.Length>0&&(args[0]=="--self-test"||args[0]=="--core-test")){Tests.Run(args.Length>1?args[1]:AppDomain.CurrentDomain.BaseDirectory,args[0]=="--core-test");return;}Application.Run(new MainForm(false));}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-error.txt"),ex.ToString());if(args.Length==0)MessageBox.Show(ex.Message,"TCKO2 Synth modular",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}}
}
}

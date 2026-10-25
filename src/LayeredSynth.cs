using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TCKO2Synth {
sealed class SoundLayer {
    internal int Id,Kind,Index;internal string Name;internal Synth Voice;
    internal bool On=true,Removing;internal double Gain=1,Level=1,Character=.5;
    public override string ToString(){return (On?"":"[muted] ")+Name;}
}
// All note input reaches this mixer: mouse, computer keys and MIDI share one ledger.
// Instrument selection adds a voice engine. Glitch/Fragments process the actual sum.
sealed class LayeredSynth : Synth {
    internal const int LayerLimit=8,VoiceBudget=16;
    readonly object mixGate=new object();readonly List<SoundLayer> layers=new List<SoundLayer>();
    sealed class Key {internal int Note,Velocity,Channel;internal bool Down=true;}
    readonly Dictionary<int,Key> keys=new Dictionary<int,Key>();readonly bool[] pedals=new bool[18];readonly double[] bends=new double[18];
    readonly SliceEffects mixEffects=new SliceEffects();SoundLayer current,technology,electro;int nextId,version;
    double master=.2,bloom=.75,echo=.85,glitchAmount=.65,fragmentsAmount=.65,glitchVolume=2,glitchSmooth,fragmentsSmooth,volumeSmooth=2,noteFrequency=220;
    bool hold,drone,glitch,fragments,stopped,clarity=true;int pitch,fadeFrames;double fadeGain=1,fadeStep;
    short[] buffer=new short[512];double[] sum=new double[512];
    internal string Message="Select sounds to add layers. Every layer follows the same keys.";
    internal int Version {get{lock(mixGate)return version;}}
    internal SoundLayer[] Layers {get{lock(mixGate)return layers.Where(x=>!x.Removing).ToArray();}}
    internal SoundLayer Current {get{lock(mixGate)return current;}}
    internal SoundLayer TechnologyLayer {get{lock(mixGate)return technology!=null&&!technology.Removing?technology:null;}}
    internal SoundLayer ElectroLayer {get{lock(mixGate)return electro!=null&&!electro.Removing?electro:null;}}
    public LayeredSynth(){new TechnologyVoice(Rate);Add(0,0);}
    static double Limit(double value,double minimum,double maximum){return Math.Max(minimum,Math.Min(maximum,value));}
    void Configure(Synth voice){voice.Volume(master);voice.SetBloom(bloom);voice.SetEchoTime(echo);voice.SetPitch(pitch);voice.SetNoteClarity(clarity);voice.SetHold(hold);voice.SetDrone(drone);voice.Stopped=stopped;for(int ch=0;ch<18;ch++){voice.Bend(ch,bends[ch]);voice.Sustain(ch,pedals[ch]);}}
    void Seed(SoundLayer layer){if(!layer.On||stopped)return;foreach(Key key in keys.Values){layer.Voice.NoteOn(key.Note,key.Velocity,key.Channel);if(!key.Down)layer.Voice.NoteOff(key.Note,key.Channel);}}
    SoundLayer Add(int kind,int index){
        lock(mixGate){
            var existing=layers.FirstOrDefault(x=>!x.Removing&&x.Kind==kind&&x.Index==index);
            if(existing!=null){current=existing;if(!existing.On)SetLayerOn(existing,true);if(kind==1)technology=existing;if(kind==2)electro=existing;version++;Message="Already in the mix: "+existing.Name;return existing;}
            int count=layers.Count(x=>!x.Removing);
            if(count>=LayerLimit||(count+1)*keys.Count>VoiceBudget){Message="Mix is full. Remove a layer or release some notes; previous sounds were kept.";return null;}
            var voice=new Synth();if(kind==1)voice.SelectTechnology(index);else voice.SetPatch(index);Configure(voice);
            var layer=new SoundLayer{Id=++nextId,Kind=kind,Index=index,Name=kind==1?TechnologyNames.Labels[index]:Patch.All[index].Name,Voice=voice,Gain=keys.Count==0?1:0};
            if(kind==1){layer.Level=.35;voice.TechnologyControls(layer.Character,layer.Level);technology=layer;}if(kind==2)electro=layer;
            layers.Add(layer);current=layer;Seed(layer);version++;Message="Added "+layer.Name+" · "+(count+1)+" layers together";return layer;
        }
    }
    internal void AddElectro(int index){Add(2,Math.Max(11,Math.Min(Patch.All.Length-1,index)));}
    public override void SetPatch(int index){if(layers==null){base.SetPatch(index);return;}lock(mixGate){if(Add(0,index)!=null&&Patch.All[index].Drone&&!hold)SetDrone(true);}}
    public override void SelectTechnology(int index){if(index>=0)Add(1,Math.Min(11,index));}
    public override int TechnologyType {get{lock(mixGate)return technology==null?-1:technology.Index;}}
    public override void TechnologyControls(double character,double level){lock(mixGate){if(technology==null)return;technology.Character=Limit(character,0,1);technology.Level=Limit(level,0,1);technology.Voice.TechnologyControls(technology.Character,technology.Level);}}
    internal void SetLayerLevel(SoundLayer layer,double level){lock(mixGate){if(layer==null||layer.Removing)return;layer.Level=Limit(level,0,1);if(layer.Kind==1)layer.Voice.TechnologyControls(layer.Character,layer.Level);}}
    internal void SetLayerOn(SoundLayer layer,bool on){lock(mixGate){if(layer==null||layer.Removing||layer.On==on)return;if(on&&(layers.Count(x=>x.On&&!x.Removing)+1)*keys.Count>VoiceBudget){Message="Release some notes before enabling more layers; previous notes were kept.";return;}layer.On=on;if(on){Configure(layer.Voice);Seed(layer);}version++;}}
    internal void SelectLayer(int id){lock(mixGate){var item=layers.FirstOrDefault(x=>x.Id==id&&!x.Removing);if(item!=null){current=item;if(item.Kind==1)technology=item;if(item.Kind==2)electro=item;}}}
    internal void RemoveLayer(int id){lock(mixGate){var item=layers.FirstOrDefault(x=>x.Id==id&&!x.Removing);if(item==null)return;item.Removing=true;item.On=false;if(current==item)current=layers.FirstOrDefault(x=>!x.Removing);if(technology==item)technology=layers.LastOrDefault(x=>!x.Removing&&x.Kind==1);if(electro==item)electro=layers.LastOrDefault(x=>!x.Removing&&x.Kind==2);version++;Message="Removed "+item.Name+"; other layers continue.";}}
    internal void ResetLayers(int patchIndex){lock(mixGate){foreach(var layer in layers)layer.Voice.Panic();layers.Clear();current=technology=electro=null;keys.Clear();mixEffects.Clear();Add(0,patchIndex);version++;}}
    public override void Set(int index,double value){lock(mixGate){if(current!=null)current.Voice.Set(index,value);}}
    public override double Get(int index){lock(mixGate)return current==null?Patch.All[0].Values[index]:current.Voice.Get(index);}
    public override bool Stopped {get{lock(mixGate)return stopped;}set{lock(mixGate){stopped=value;foreach(var layer in layers)layer.Voice.Stopped=value;}}}
    public override void Volume(double value){lock(mixGate){master=Limit(value,0,.8);foreach(var layer in layers)layer.Voice.Volume(master);}}
    public override void SetBloom(double value){lock(mixGate){bloom=Limit(value,0,1);foreach(var layer in layers)layer.Voice.SetBloom(bloom);}}
    public override double BloomAmount{get{lock(mixGate)return bloom;}}
    public override void SetEchoTime(double value){lock(mixGate){echo=Limit(value,.08,4);foreach(var layer in layers)layer.Voice.SetEchoTime(echo);}}
    public override double EchoTime{get{lock(mixGate)return echo;}}
    public override void SetPitch(int value){lock(mixGate){pitch=Math.Max(-12,Math.Min(12,value));foreach(var layer in layers)layer.Voice.SetPitch(pitch);}}
    public override int PitchSemitones{get{lock(mixGate)return pitch;}}
    public override void SetNoteClarity(bool value){lock(mixGate){clarity=value;foreach(var layer in layers)layer.Voice.SetNoteClarity(value);}}
    public override bool NoteClarity{get{lock(mixGate)return clarity;}}
    public override bool HoldMode{get{lock(mixGate)return hold;}}public override bool DroneMode{get{lock(mixGate)return drone;}}
    void ReleaseUnheld(){foreach(int id in keys.Where(pair=>!pair.Value.Down&&!hold&&!drone&&!pedals[pair.Value.Channel]).Select(pair=>pair.Key).ToArray())keys.Remove(id);}
    public override void SetHold(bool value){lock(mixGate){hold=value;if(value)drone=false;foreach(var layer in layers)layer.Voice.SetHold(value);ReleaseUnheld();}}
    public override void SetDrone(bool value){lock(mixGate){drone=value;if(value)hold=false;foreach(var layer in layers)layer.Voice.SetDrone(value);ReleaseUnheld();}}
    public override void Bend(int ch,double value){lock(mixGate){bends[ch]=value;foreach(var layer in layers)layer.Voice.Bend(ch,value);}}
    public override void Sustain(int ch,bool value){lock(mixGate){pedals[ch]=value;foreach(var layer in layers)layer.Voice.Sustain(ch,value);ReleaseUnheld();}}
    public override void ReleaseChannel(int ch){lock(mixGate){pedals[ch]=false;foreach(int id in keys.Where(x=>x.Value.Channel==ch).Select(x=>x.Key).ToArray())keys.Remove(id);foreach(var layer in layers)layer.Voice.ReleaseChannel(ch);}}
    public override void ResetChannel(int ch){lock(mixGate){Bend(ch,0);Sustain(ch,false);}}
    public override void ReleaseKeys(int ch){lock(mixGate){foreach(var key in keys.Values.Where(x=>x.Channel==ch).ToArray())NoteOff(key.Note,ch);}}
    public override bool IsNote(int note){lock(mixGate)return keys.Values.Any(x=>x.Note==note);}
    public override void NoteOn(int note,int velocity,int ch){lock(mixGate){if(velocity==0){NoteOff(note,ch);return;}if(stopped||note<0||note>127||ch<0||ch>=18)return;int id=ch*128+note;Key existing;
        if(drone&&keys.TryGetValue(id,out existing)&&!existing.Down){keys.Remove(id);foreach(var layer in layers)if(layer.On&&!layer.Removing){layer.Voice.NoteOn(note,velocity,ch);layer.Voice.NoteOff(note,ch);}return;}
        int count=layers.Count(x=>x.On&&!x.Removing);if(count==0){Message="Choose a sound to add a layer.";return;}
        if(!keys.ContainsKey(id)&&(keys.Count>=8||(keys.Count+1)*count>VoiceBudget)){Message="All mix voices are in use. Release notes or remove a layer; previous notes were kept.";return;}
        keys[id]=new Key{Note=note,Velocity=velocity,Channel=ch};fadeFrames=0;if(fadeGain<1)fadeGain=1;noteFrequency=440*Math.Pow(2,(note-69)/12.0);
        foreach(var layer in layers)if(layer.On&&!layer.Removing)layer.Voice.NoteOn(note,velocity,ch);
    }}
    public override void NoteOff(int note,int ch){lock(mixGate){Key key;if(keys.TryGetValue(ch*128+note,out key))key.Down=false;foreach(var layer in layers)layer.Voice.NoteOff(note,ch);ReleaseUnheld();}}
    public override void Panic(){lock(mixGate){foreach(var layer in layers)layer.Voice.Panic();keys.Clear();Array.Clear(pedals,0,pedals.Length);Array.Clear(bends,0,bends.Length);hold=drone=glitch=fragments=false;glitchSmooth=fragmentsSmooth=0;mixEffects.Clear();fadeFrames=0;fadeGain=1;Peak=0;ActiveCount=0;}}
    public override void FadeOut(double seconds){lock(mixGate){fadeFrames=Math.Max(1,(int)(Limit(seconds,1,12)*Rate));fadeStep=fadeGain/fadeFrames;}}
    public override bool Fading{get{lock(mixGate)return fadeFrames>0;}}public override bool Faded{get{lock(mixGate)return fadeGain==0;}}
    public override bool GlitchMode{get{lock(mixGate)return glitch;}}public override bool FragmentsMode{get{lock(mixGate)return fragments;}}
    public override void SetGlitch(bool value){lock(mixGate)glitch=value;}public override void SetFragments(bool value){lock(mixGate)fragments=value;}
    public override double GlitchAmount{get{lock(mixGate)return glitchAmount;}}public override double FragmentsAmount{get{lock(mixGate)return fragmentsAmount;}}
    public override void SetGlitchAmount(double value){lock(mixGate)glitchAmount=Limit(value,0,1);}public override void SetFragmentsAmount(double value){lock(mixGate)fragmentsAmount=Limit(value,0,1);}
    GlitchType glitchType;public override GlitchType SelectedGlitchType{get{lock(mixGate)return glitchType;}}
    public override void SetGlitchType(GlitchType value){lock(mixGate){if(glitchType!=value){glitchType=value;mixEffects.ResetGlitch();}}}
    public override double GlitchVolume{get{lock(mixGate)return glitchVolume;}}public override void SetGlitchVolume(double value){lock(mixGate)glitchVolume=Limit(value,0,4);}
    static double Ceiling(double value){double magnitude=Math.Abs(value);return magnitude<=.8?value:Math.Sign(value)*(.8+.15*(1-Math.Exp(-(magnitude-.8)/.15)));}
    public override void Render(short[] pcm,int frames){lock(mixGate){
        if(buffer.Length<frames*2){buffer=new short[frames*2];sum=new double[frames*2];}Array.Clear(sum,0,frames*2);ActiveCount=0;
        foreach(var layer in layers){if(!layer.On&&layer.Gain<=0)continue;layer.Voice.Render(buffer,frames);ActiveCount+=layer.Voice.ActiveCount;InvalidSamples+=layer.Voice.InvalidSamples;double target=layer.On&&!layer.Removing?1:0;
            for(int frame=0;frame<frames;frame++){layer.Gain+=.006*(target-layer.Gain);if(target==0&&layer.Gain<.000001)layer.Gain=0;double level=layer.Level==0?0:layer.Kind==1?1:layer.Level;sum[frame*2]+=buffer[frame*2]/32768.0*layer.Gain*level;sum[frame*2+1]+=buffer[frame*2+1]/32768.0*layer.Gain*level;}
            if(!layer.On&&layer.Gain==0)layer.Voice.Panic();
        }
        layers.RemoveAll(x=>x.Removing&&x.Gain==0);double peak=0;
        for(int frame=0;frame<frames;frame++){
            double left=sum[frame*2],right=sum[frame*2+1];glitchSmooth+=.0018*((glitch?glitchAmount:0)-glitchSmooth);fragmentsSmooth+=.0018*((fragments?fragmentsAmount:0)-fragmentsSmooth);volumeSmooth+=.0018*(glitchVolume-volumeSmooth);
            mixEffects.Process(ref left,ref right,glitchSmooth,fragmentsSmooth,glitchType,volumeSmooth,clarity?noteFrequency:220);
            if(fadeFrames>0){fadeGain=Math.Max(0,fadeGain-fadeStep);if(--fadeFrames==0){Panic();fadeGain=0;}}
            left=stopped||master==0?0:Ceiling(left*fadeGain);right=stopped||master==0?0:Ceiling(right*fadeGain);
            if(double.IsNaN(left)||double.IsInfinity(left)||double.IsNaN(right)||double.IsInfinity(right)){InvalidSamples++;Panic();left=right=0;}
            pcm[frame*2]=(short)Math.Round(left*32767);pcm[frame*2+1]=(short)Math.Round(right*32767);peak=Math.Max(peak,Math.Max(Math.Abs(left),Math.Abs(right)));
        }Peak=(float)peak;
    }}
    internal void SaveLayers(List<string> values){lock(mixGate){values.Add("layerFormat=1");var kept=layers.Where(x=>!x.Removing).ToList();values.Add("layerCurrent="+kept.IndexOf(current));values.Add("layerTechnology="+kept.IndexOf(technology));values.Add("layerElectro="+kept.IndexOf(electro));int index=0;foreach(var layer in kept){var fields=new List<int>{layer.Kind,layer.Index,layer.On?1:0,(int)Math.Round(layer.Level*1000),(int)Math.Round(layer.Character*1000)};for(int i=0;i<8;i++)fields.Add((int)Math.Round(layer.Voice.Get(i)*10000));values.Add("layer"+(index++)+"="+String.Join(",",fields));}}}
    SoundLayer RestoreSelection(string[] lines,string name,SoundLayer fallback){string prefix=name+"=";string line=lines.FirstOrDefault(x=>x.StartsWith(prefix,StringComparison.Ordinal));int index;if(line!=null&&Int32.TryParse(line.Substring(prefix.Length),out index)&&index>=0&&index<layers.Count)return layers[index];return fallback;}
    internal bool RestoreLayers(string[] lines){
        if(!lines.Contains("layerFormat=1"))return false;
        var parsed=new List<int[]>();for(int index=0;index<LayerLimit;index++){string prefix="layer"+index+"=";string line=lines.FirstOrDefault(x=>x.StartsWith(prefix,StringComparison.Ordinal));if(line==null)break;string[] parts=line.Substring(prefix.Length).Split(',');var values=new int[13];if(parts.Length!=13||parts.Where((x,i)=>!Int32.TryParse(x,out values[i])).Any())return false;
            if(values[0]<0||values[0]>2||values[1]<0||values[1]>=(values[0]==1?12:Patch.All.Length)||values[2]<0||values[2]>1||values[3]<0||values[3]>1000||values[4]<0||values[4]>1000||values.Skip(5).Any(x=>x<0||x>10000))return false;parsed.Add(values);}
        lock(mixGate){bool keepGlitch=glitch,keepFragments=fragments,keepDrone=drone;Panic();glitch=keepGlitch;fragments=keepFragments;drone=keepDrone;layers.Clear();current=technology=electro=null;foreach(var value in parsed){var layer=Add(value[0],value[1]);if(layer==null)continue;layer.Level=value[3]/1000.0;layer.Character=value[4]/1000.0;for(int i=0;i<8;i++)layer.Voice.Set(i,value[5+i]/10000.0);SetLayerLevel(layer,layer.Level);SetLayerOn(layer,value[2]==1);}current=RestoreSelection(lines,"layerCurrent",current);technology=RestoreSelection(lines,"layerTechnology",technology);electro=RestoreSelection(lines,"layerElectro",electro);version++;return true;}
    }
}
}


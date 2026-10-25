using System;

namespace TCKO2Synth {
static class TechnologyTables {
    internal static double[] Source,Frozen,A,B;
}
// Bounded, allocation-free sample loop. All source material is original synthesis.
// This arithmetic is mirrored in the browser implementation and checked against its PCM output.
sealed class TechnologyVoice {
    public bool Playing=false;
    public double Left=0,Right=0;
    public int Type=0,Note=48;
    public double Character=.5,Level=.35;
    double rate,frequency,phase=0,time=0,seed=123456789,gain=0,fade=1,fadeStep=0;
    double low=0,previous=0,slip=0,walk=0,burst=0,nextEvent=0;
    int cursor=0,pluckLength=100,grainSlot=0,tableStart=0;
    double[] source,frozen,tableA,tableB;
    double[] pluck=new double[4096],history=new double[256],impulse=new double[128];
    double[] mode1=new double[8],mode2=new double[8],modeC=new double[8],modeR=new double[8];
    double[] ages=new double[16],lengths=new double[16],starts=new double[16],speeds=new double[16],pans=new double[16],osc=new double[16];
    double[] ratios=new double[]{1,1.593,2.135,2.917,3.598,4.421,5.713,7.271};
    public TechnologyVoice(double sampleRate){rate=sampleRate;if(TechnologyTables.Source==null){source=new double[2048];frozen=new double[16384];tableA=new double[16384];tableB=new double[16384];Prepare();TechnologyTables.Source=source;TechnologyTables.Frozen=frozen;TechnologyTables.A=tableA;TechnologyTables.B=tableB;}else{source=TechnologyTables.Source;frozen=TechnologyTables.Frozen;tableA=TechnologyTables.A;tableB=TechnologyTables.B;}Reset();}
    double Random(){seed=(seed*1664525+1013904223)%4294967296.0;return seed/4294967296.0;}
    double Noise(){return Random()*2-1;}
    int Index(double value){return (int)value;}
    double Clamp(double x,double a,double b){return Math.Max(a,Math.Min(b,x));}
    double Read(double[] data,double index){index=index%data.Length;if(index<0)index+=data.Length;int i=(int)index;double a=index-i;return data[i]*(1-a)+data[(i+1)%data.Length]*a;}
    public void Configure(int selected,int note,double character,double level){
        selected=Index(Clamp(selected,0,11));note=Index(Clamp(note,0,127));
        bool restart=selected!=Type||note!=Note;
        Type=selected;Note=note;Character=Clamp(character,0,1);Level=Clamp(level,0,1);
        if(restart)Reset();
    }
    public void Play(bool enabled){Playing=enabled;if(enabled){Reset();fade=1;fadeStep=0;}}
    public void Repeat(){if(Playing)Reset();}
    public void Panic(){Playing=false;gain=0;Left=Right=0;fade=1;fadeStep=0;}
    public void FadeOut(double seconds){fadeStep=1/(rate*Clamp(seconds,1,12));}
    void Prepare(){
        for(int i=0;i<2048;i++){
            double t=i/2048.0;
            source[i]=(Math.Sin(2*Math.PI*9*t)+.5*Math.Sin(2*Math.PI*23*t)+Noise()*.25)*Math.Exp(-t*7);
        }
        // A captured magnitude spectrum of a generated impact, held for Freeze.
        // Morph interpolates two phase-aligned spectral envelopes, not playback speed.
        for(int k=1;k<=48;k++){
            double re=0,im=0;
            for(int i=0;i<2048;i++){double a=2*Math.PI*k*i/2048;re+=source[i]*Math.Cos(a);im-=source[i]*Math.Sin(a);}
            double magnitude=2*Math.Sqrt(re*re+im*im)/2048;
            for(int i=0;i<2048;i++){
                double wave=Math.Sin(2*Math.PI*k*i/2048);
                for(int octave=0;octave<8;octave++)if(k<=Math.Max(1,48/Math.Pow(2,octave))){int at=octave*2048+i;frozen[at]+=wave*magnitude;tableA[at]+=wave/(k*k);tableB[at]+=wave*(k%3==0?.65/k:.08/k);}
            }
        }
    }
    double Table(double[] data){double index=phase*2048;int i=(int)index;double a=index-i;return data[tableStart+i]*(1-a)+data[tableStart+(i+1)%2048]*a;}
    void Reset(){
        frequency=440*Math.Pow(2,(Note-69)/12.0);phase=time=0;cursor=grainSlot=0;seed=123456789;
        tableStart=2048*Index(Clamp(Math.Ceiling(Math.Log(frequency*48/(rate*.42))/Math.Log(2)),0,7));
        nextEvent=burst=low=previous=slip=walk=0;fade=1;fadeStep=0;
        for(int i=0;i<16;i++){ages[i]=lengths[i]=osc[i]=0;}
        for(int i=0;i<4096;i++)pluck[i]=0;
        for(int i=0;i<256;i++)history[i]=0;
        pluckLength=Index(Clamp(rate/frequency,3,4095));
        for(int i=0;i<pluckLength;i++)pluck[i]=Noise()*.7;
        for(int i=0;i<8;i++){
            double f=Math.Min(rate*.42,frequency*ratios[i]);
            modeR[i]=Math.Exp(-1/(rate*(.15+1.1/(i+1))));
            modeC[i]=2*modeR[i]*Math.Cos(2*Math.PI*f/rate);mode1[i]=mode2[i]=0;
        }
        for(int i=0;i<128;i++)impulse[i]=(Math.Sin(i*.73*frequency/130.8128)+.5*Math.Cos(i*1.37*frequency/130.8128))*Math.Exp(-i/22.0)*.07;
    }
    double Resonance(double excitation){double sum=0;for(int i=0;i<8;i++){double y=excitation*.035/(i+1)+modeC[i]*mode1[i]-modeR[i]*modeR[i]*mode2[i];mode2[i]=mode1[i];mode1[i]=y;sum+=y;}return sum*.25;}
    public void Next(){
        Left=Right=0;if(!Playing&&gain<.000001){gain=0;return;}
        gain+=.002*((Playing?Level:0)-gain);
        if(fadeStep>0){fade=Math.Max(0,fade-fadeStep);if(fade==0){Panic();return;}}
        double x=0,pan=0,c=Character,n=Noise();
        phase+=frequency/rate;if(phase>=1)phase-=1;
        time+=1/rate;
        switch(Type){
        case 0:
            if(nextEvent--<=0){int g=grainSlot++%16;ages[g]=0;lengths[g]=rate*(.018+.09*Random());starts[g]=Random()*1700;speeds[g]=(.3+Random()*2)*(Random()<.3?-1:1)*frequency/130.8128;pans[g]=Noise();nextEvent=rate/(6+c*90);}
            double l=0,r=0;
            for(int g=0;g<16;g++)if(ages[g]<lengths[g]){double envelope=.5-.5*Math.Cos(2*Math.PI*ages[g]/lengths[g]);double s=Read(source,starts[g]+ages[g]*speeds[g])*envelope*.35;l+=s*Math.Sqrt((1-pans[g])*.5);r+=s*Math.Sqrt((1+pans[g])*.5);ages[g]++;}
            Left=l;Right=r;break;
        case 1:
            if(nextEvent--<=0){for(int i=0;i<pluckLength;i++)pluck[i]=Noise()*.7;nextEvent=rate*(1.2+.7*c);}
            int p=cursor%pluckLength;double value=pluck[p];pluck[p]=(value+pluck[(p+1)%pluckLength])*(.488+.011*c);cursor++;x=value*.5;break;
        case 2: x=Table(frozen)*(.6+.2*Math.Sin(time*(.1+c)));pan=Math.Sin(time*(.05+c*.4))*.6;break;
        case 3:
            double hit=0;if(nextEvent--<=0){hit=1;nextEvent=rate*(.2+Random()*(1.2-c));}x=Resonance(hit);break;
        case 4:
            slip+=(.0004+c*.005)*(1+.5*n);double force=0;if(slip>.025+Random()*.035){force=slip*12;slip*=.1;}low+=.03*(n-low);x=Resonance(force+(n-low)*(.08+c*.2))*.06;break;
        case 5:
            if(nextEvent--<=0){int g=grainSlot++%16;ages[g]=0;lengths[g]=rate*(.04+Random()*.22);starts[g]=frequency*(.7+Random()*4);osc[g]=0;pans[g]=Noise();nextEvent=rate/(2+c*35);}
            for(int g=0;g<16;g++)if(ages[g]<lengths[g]){double t=ages[g]/lengths[g];osc[g]+=2*Math.PI*Math.Min(rate*.42,starts[g]*(1+t*.65))/rate;double s=Math.Sin(osc[g])*Math.Exp(-t*6)*.32;Left+=s*Math.Sqrt((1-pans[g])*.5);Right+=s*Math.Sqrt((1+pans[g])*.5);ages[g]++;}break;
        case 6:
            // Two sub-samples soften the folded edges before output filtering.
            for(int j=0;j<2;j++){double a=Math.Sin(2*Math.PI*(phase+j*frequency/rate*.5))*(1+c*9);double folded=2*Math.Abs(2*(a/4-Math.Floor(a/4+.5)))-1;x+=folded*.4;}low+=.38*(x-low);x=low;break;
        case 7:
            if(nextEvent--<=0){burst=1;nextEvent=rate*(.7+c);}
            burst*=.99995;for(int k=1;k<=7;k++){double f=frequency*ratios[k-1]+(c*2-1)*173;if(Math.Abs(f)<rate*.42)x+=Math.Sin(2*Math.PI*f*time)/(k*k);}x*=.36*burst;break;
        case 8:
            burst*=.995;if(nextEvent--<=0){burst=1;nextEvent=rate*(.2+c*.9);}history[cursor%256]=n*burst;
            for(int k=0;k<128;k++){int back=(cursor-k+256)%256;x+=history[back]*impulse[k];}cursor=(cursor+1)%256;x*=1.5;break;
        case 9:
            if(nextEvent--<=0){walk=Clamp(walk+Noise()*.35,-1,1);burst=.4+Random()*.6;nextEvent=rate*(.025+Random()*(.5-.43*c));}burst*=.999;osc[0]+=frequency*Math.Pow(2,walk*2)/rate;osc[0]%=1;x=Math.Sin(2*Math.PI*osc[0])*burst*.4+n*burst*.06;pan=walk;break;
        case 10: low+=.02*(n-low);x=Resonance((n-low)*.06)*.16;pan=Math.Sin(time*(.3+c*5));break;
        case 11:
            double morph=Clamp(c+.3*Math.Sin(time*.5),0,1);x=(Table(tableA)*(1-morph)+Table(tableB)*morph)*.45;break;
        }
        if(Type!=0&&Type!=5){Left=x*Math.Sqrt((1-pan)*.5);Right=x*Math.Sqrt((1+pan)*.5);}
        // DC blocker also removes offsets introduced by asymmetric wavefolding.
        double mono=(Left+Right)*.5;previous+=.0008*(mono-previous);Left-=previous;Right-=previous;
        Left=Clamp(Left*gain*fade,-.8,.8);Right=Clamp(Right*gain*fade,-.8,.8);
    }
}
static class TechnologyNames {
    internal static readonly string[] Labels={"Granular / grains","Vibrating objects","Frozen spectrum","Resonant metal","Friction / scrape","Liquid / bubbles","Wavefold","Frequency shift","Inside an object","Broken / random","Moving in stereo","Spectral morph"};
    internal static readonly string[] Names={"Granular synthesis","Physical modelling","Spectral freezing","Modal resonance","Friction synthesis","Liquid simulation","Wavefolding","Frequency shifting","Convolution processing","Stochastic synthesis","Spatial modulation","Spectral morphing"};
    internal static readonly string[] Descriptions={"Pollock: scattered, windowed grains of an original impact.","Plucked digital waveguide: vibrating strings, from damp wood to ringing material.","Rothko: the held magnitude spectrum of an original impact.","Eight inharmonic resonators: struck metal, plates and stone.","Stick-slip excitation: rubbing and grinding a resonant surface.","Damped, rising bubble oscillators: drops and molten liquid.","A sine folded across multiple thresholds: electronic chaos.","Inharmonic metal partials shifted by the same frequency offset.","An original impact convolved with a short imaginary-object impulse response.","Seeded random events and a bounded pitch walk: broken beats.","A resonant scraping surface travelling through stereo space.","Interpolation between two original spectral envelopes: solid into liquid."};
}
}


using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace TCKO2Synth {
public sealed class AudioClip {
    public readonly float[] Samples;
    public readonly string Name;
    public readonly float[] Peaks;
    public int Frames { get { return Samples.Length / 2; } }
    public double Seconds { get { return Frames / 44100.0; } }
    public AudioClip(string name, float[] samples) {
        Name = name; Samples = samples; Peaks = new float[Math.Min(4096, Frames)];
        for (int frame = 0; frame < Frames; frame++) { int bin = (int)((long)frame * Peaks.Length / Frames); Peaks[bin] = Math.Max(Peaks[bin], Math.Max(Math.Abs(samples[frame * 2]), Math.Abs(samples[frame * 2 + 1]))); }
    }
}

public static class Wav {
    public const int Rate = 44100;
    public static AudioClip Read(string path) { using (var stream = File.OpenRead(path)) return Read(stream, Path.GetFileNameWithoutExtension(path)); }
    public static AudioClip Read(Stream stream, string name) {
        using (var reader = new BinaryReader(stream, Encoding.UTF8, true)) {
            if (stream.Length > 512L * 1024 * 1024) throw new IOException("Use a WAV smaller than 512 MB per channel.");
            if (new string(reader.ReadChars(4)) != "RIFF") throw new IOException("Choose a standard PCM or float WAV file.");
            uint riffBytes = reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE" || riffBytes + 8L > stream.Length) throw new IOException("Incomplete WAV file.");
            int format = 0, channels = 0, rate = 0, bits = 0, alignment = 0; long dataAt = -1, dataBytes = 0;
            while (stream.Position + 8 <= stream.Length) {
                string chunk = new string(reader.ReadChars(4)); uint size = reader.ReadUInt32(); long start = stream.Position;
                if (size > stream.Length - start) throw new IOException("Truncated WAV chunk.");
                if (chunk == "fmt ") {
                    if (size < 16) throw new IOException("Invalid WAV format.");
                    format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); alignment = reader.ReadUInt16(); bits = reader.ReadUInt16();
                    if (format == 65534 && size >= 40) {
                        reader.ReadUInt16(); reader.ReadUInt16(); reader.ReadUInt32(); string subtype = new Guid(reader.ReadBytes(16)).ToString();
                        format = subtype == "00000001-0000-0010-8000-00aa00389b71" ? 1 : subtype == "00000003-0000-0010-8000-00aa00389b71" ? 3 : 0;
                    }
                } else if (chunk == "data" && dataAt < 0) { dataAt = start; dataBytes = size; }
                stream.Position = start + size + (size & 1);
            }
            if ((format != 1 && format != 3) || channels < 1 || channels > 2 || rate < 8000 || rate > 192000 || dataAt < 0 || alignment != channels * bits / 8 || (format == 1 && bits != 8 && bits != 16 && bits != 24 && bits != 32) || (format == 3 && bits != 32 && bits != 64))
                throw new IOException("Supported: mono/stereo PCM 8/16/24/32-bit or float 32/64-bit WAV, 8–192 kHz. Export WAV from TCKO2; compressed audio is not supported yet.");
            if (dataBytes == 0 || dataBytes % alignment != 0) throw new IOException("The WAV contains no complete audio frames.");
            long frames = dataBytes / alignment, converted = (long)Math.Round(frames * (double)Rate / rate);
            if (converted < 1 || converted > Rate * 60L * 30 || frames > int.MaxValue / 2) throw new IOException("Use takes up to 30 minutes long.");
            float[] original = new float[checked((int)frames * 2)]; stream.Position = dataAt;
            for (int frame = 0; frame < frames; frame++) {
                float left = Sample(reader, format, bits), right = channels == 2 ? Sample(reader, format, bits) : left;
                original[frame * 2] = left; original[frame * 2 + 1] = right;
            }
            if (rate == Rate) return new AudioClip(name, original);
            float[] samples = new float[checked((int)converted * 2)];
            for (int frame = 0; frame < converted; frame++) {
                double position = frame * (double)rate / Rate; int first = Math.Min((int)frames - 1, (int)position), second = Math.Min((int)frames - 1, first + 1); double fraction = position - first;
                for (int channel = 0; channel < 2; channel++) samples[frame * 2 + channel] = (float)(original[first * 2 + channel] * (1 - fraction) + original[second * 2 + channel] * fraction);
            }
            return new AudioClip(name, samples);
        }
    }
    static float Sample(BinaryReader reader, int format, int bits) {
        double value;
        if (format == 3) value = bits == 32 ? reader.ReadSingle() : reader.ReadDouble();
        else if (bits == 8) value = (reader.ReadByte() - 128) / 128.0;
        else if (bits == 16) value = reader.ReadInt16() / 32768.0;
        else if (bits == 24) { int packed = reader.ReadByte() | reader.ReadByte() << 8 | reader.ReadByte() << 16; if ((packed & 0x800000) != 0) packed |= unchecked((int)0xff000000); value = packed / 8388608.0; }
        else value = reader.ReadInt32() / 2147483648.0;
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : (float)Math.Max(-1, Math.Min(1, value));
    }
    public static void Write(Stream stream, long frames, Func<long, int, double> sample) {
        if (frames < 0 || frames > (uint.MaxValue - 36L) / 4) throw new IOException("Mix is too long for a WAV file.");
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((uint)(frames * 4 + 36)); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(Rate); writer.Write(Rate * 4); writer.Write((ushort)4); writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((uint)(frames * 4));
            for (long frame = 0; frame < frames; frame++) for (int channel = 0; channel < 2; channel++) writer.Write((short)Math.Round(Math.Max(-1, Math.Min(1, sample(frame, channel))) * 32767));
        }
    }
}

public sealed class DesktopPcmConverter {
    readonly int rate,channels,bits,format,align;
    readonly uint mask;
    readonly Action<short[],int> receive;
    readonly short[] output=new short[2048];
    int used;
    long sourceFrames;
    double next, previousLeft, previousRight;
    public long Frames;
    public float Peak;
    public int SourceRate { get { return rate; } }
    public DesktopPcmConverter(int sampleRate,int channelCount,int bitDepth,int encoding,int blockAlign,uint channelMask,Action<short[],int> sink) {
        rate=sampleRate; channels=channelCount; bits=bitDepth; format=encoding; align=blockAlign; mask=channelMask; receive=sink;
        if(rate<8000 || rate>384000 || channels<1 || channels>32 || align!=channels*bits/8 || (format!=1 && format!=3) || (format==3 && bits!=32 && bits!=64) || (format==1 && bits!=16 && bits!=24 && bits!=32)) throw new IOException("Unsupported desktop playback format. Capture did not use another audio source.");
    }
    double Read(byte[] data,int offset) {
        double value;
        if(format==3) value=bits==32 ? BitConverter.ToSingle(data,offset) : BitConverter.ToDouble(data,offset);
        else if(bits==16) value=BitConverter.ToInt16(data,offset)/32768.0;
        else if(bits==32) value=BitConverter.ToInt32(data,offset)/2147483648.0;
        else { int v=data[offset] | data[offset+1]<<8 | data[offset+2]<<16; if((v&0x800000)!=0) v|=unchecked((int)0xff000000); value=v/8388608.0; }
        return double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(-1,Math.Min(1,value));
    }
    void Frame(double left,double right) {
        while(next<=sourceFrames) {
            double fraction=sourceFrames==0 ? 1 : Math.Max(0,Math.Min(1,next-(sourceFrames-1)));
            double l=previousLeft+(left-previousLeft)*fraction, r=previousRight+(right-previousRight)*fraction;
            l=Math.Max(-1,Math.Min(1,l)); r=Math.Max(-1,Math.Min(1,r));
            output[used++]=(short)Math.Round(l*32767); output[used++]=(short)Math.Round(r*32767);
            Peak=(float)Math.Max(Math.Abs(l),Math.Abs(r)); Frames++;
            if(used==output.Length) Flush();
            next+=rate/(double)Wav.Rate;
        }
        previousLeft=left; previousRight=right; sourceFrames++;
    }
    public void Silence(long count) { for(long i=0;i<count;i++) Frame(0,0); }
    public void Push(byte[] data,int frameCount,int skip=0) {
        for(int frame=skip;frame<frameCount;frame++) {
            double left=0,right=0;
            if(channels==1) left=right=Read(data,frame*align);
            else if(channels==2) { left=Read(data,frame*align); right=Read(data,frame*align+bits/8); }
            else {
                uint remaining=mask;
                for(int c=0;c<channels;c++) {
                    double sample=Read(data,frame*align+c*bits/8); uint speaker=remaining & (~remaining+1); remaining &= ~speaker;
                    if(mask==0) { if(c==0) left+=sample; else if(c==1) right+=sample; else { left+=sample*.25; right+=sample*.25; } }
                    else if(speaker==1) left+=sample;
                    else if(speaker==2) right+=sample;
                    else if(speaker==4) { left+=sample*.707; right+=sample*.707; }
                    else if(speaker==8) { left+=sample*.25; right+=sample*.25; }
                    else if(speaker==16 || speaker==64 || speaker==512) left+=sample*.5;
                    else if(speaker==32 || speaker==128 || speaker==1024) right+=sample*.5;
                    else { left+=sample*.25; right+=sample*.25; }
                }
            }
            Frame(left,right);
        }
    }
    public void Flush() { if(used>0) { receive(output,used/2); used=0; } }
}

// Windows Media Foundation decodes the first audio stream to PCM in memory.
// It opens neither an output device nor a player, and never writes the source.
internal static class MediaFileDecoder {
    const uint Audio=0xfffffffd,All=0xfffffffe;
    static readonly Guid Major=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"),Subtype=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"),AudioMajor=new Guid("73647561-0000-0010-8000-00aa00389b71"),Pcm=new Guid("00000001-0000-0010-8000-00aa00389b71");
    static readonly Guid Channels=new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a"),Rate=new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba"),Bits=new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669"),Align=new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
    [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFStartup(uint version,uint flags);
    [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll")] static extern void CoUninitialize();
    [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFShutdown();
    [DllImport("mfplat.dll",ExactSpelling=true)] static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int MFCreateSourceReaderFromURL(string url,IntPtr attributes,out Reader reader);
    // IMFAttributes' 30 methods precede the methods of derived Media Foundation interfaces.
    [ComImport,Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Attributes {
        void GetItem();void GetItemType();void CompareItem();void Compare();
        [PreserveSig] int GetUINT32(ref Guid key,out uint value);
        void GetUINT64();void GetDouble();
        [PreserveSig] int GetGUID(ref Guid key,out Guid value);
        void GetStringLength();void GetString();void GetAllocatedString();void GetBlobSize();void GetBlob();void GetAllocatedBlob();void GetUnknown();void SetItem();void DeleteItem();void DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid key,uint value);
        void SetUINT64();void SetDouble();
        [PreserveSig] int SetGUID(ref Guid key,ref Guid value);
        void SetString();void SetBlob();void SetUnknown();void LockStore();void UnlockStore();void GetCount();void GetItemByIndex();void CopyAllItems();
    }
    [ComImport,Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Reader {
        [PreserveSig] int GetStreamSelection(uint stream,out int selected);
        [PreserveSig] int SetStreamSelection(uint stream,int selected);
        [PreserveSig] int GetNativeMediaType(uint stream,uint index,out IntPtr type);
        [PreserveSig] int GetCurrentMediaType(uint stream,out IntPtr type);
        [PreserveSig] int SetCurrentMediaType(uint stream,IntPtr reserved,IntPtr type);
        void SetCurrentPosition();
        [PreserveSig] int ReadSample(uint stream,uint control,out uint actual,out uint flags,out long timestamp,out IntPtr sample);
    }
    [ComImport,Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Sample {
        // Flatten IMFAttributes so ConvertToContiguousBuffer occupies its native slot.
        void GetItem();void GetItemType();void CompareItem();void Compare();void GetUINT32();void GetUINT64();void GetDouble();void GetGUID();void GetStringLength();void GetString();void GetAllocatedString();void GetBlobSize();void GetBlob();void GetAllocatedBlob();void GetUnknown();void SetItem();void DeleteItem();void DeleteAllItems();void SetUINT32();void SetUINT64();void SetDouble();void SetGUID();void SetString();void SetBlob();void SetUnknown();void LockStore();void UnlockStore();void GetCount();void GetItemByIndex();void CopyAllItems();
        void GetSampleFlags();void SetSampleFlags();void GetSampleTime();void SetSampleTime();void GetSampleDuration();void SetSampleDuration();void GetBufferCount();void GetBufferByIndex();
        [PreserveSig] int ConvertToContiguousBuffer(out MediaBuffer buffer);
    }
    [ComImport,Guid("045fa593-8799-42b8-bc8d-8968c6453507"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface MediaBuffer {
        [PreserveSig] int Lock(out IntPtr data,out uint maximum,out uint current);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint length);
        [PreserveSig] int SetCurrentLength(uint length);
        [PreserveSig] int GetMaxLength(out uint length);
    }
    sealed class TypeInfo : IDisposable {
        internal IntPtr Pointer;internal Attributes Values;
        internal TypeInfo(IntPtr pointer) {Pointer=pointer;Values=(Attributes)Marshal.GetTypedObjectForIUnknown(pointer,typeof(Attributes));}
        internal uint Number(Guid key) {uint value;Check(Values.GetUINT32(ref key,out value));return value;}
        internal Guid Id(Guid key) {Guid value;Check(Values.GetGUID(ref key,out value));return value;}
        internal void Set(Guid key,Guid value) {Check(Values.SetGUID(ref key,ref value));}
        internal void Set(Guid key,uint value) {Check(Values.SetUINT32(ref key,value));}
        public void Dispose() {Release(Values);Values=null;if(Pointer!=IntPtr.Zero) {Marshal.Release(Pointer);Pointer=IntPtr.Zero;}}
    }
    static void Check(int result) {if(result<0) Marshal.ThrowExceptionForHR(result);}
    static void Release(object value) {if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);}
    public static bool Probe(string path) {
        Reader reader=null;bool started=false,com=false;
        try {
            int init=CoInitializeEx(IntPtr.Zero,0);com=init>=0;if(init<0 && init!=unchecked((int)0x80010106)) Check(init);
            Check(MFStartup(0x20070,0));started=true;Check(MFCreateSourceReaderFromURL(path,IntPtr.Zero,out reader));IntPtr type;Check(reader.GetNativeMediaType(Audio,0,out type));
            using(var native=new TypeInfo(type)) return native.Id(Major)==AudioMajor;
        } catch(COMException) {return false;} catch(DllNotFoundException) {return false;} catch(EntryPointNotFoundException) {return false;}
        finally {Release(reader);if(started) MFShutdown();if(com) CoUninitialize();}
    }
    public static AudioClip Read(string path) {
        Reader reader=null;bool started=false,com=false;
        try {
            int init=CoInitializeEx(IntPtr.Zero,0);com=init>=0;if(init<0 && init!=unchecked((int)0x80010106)) Check(init);
            var file=new FileInfo(path);if(!file.Exists || file.Length==0 || file.Length>512L*1024*1024) throw new IOException("Choose a sound file smaller than 512 MB.");
            Check(MFStartup(0x20070,0));started=true;Check(MFCreateSourceReaderFromURL(path,IntPtr.Zero,out reader));Check(reader.SetStreamSelection(All,0));Check(reader.SetStreamSelection(Audio,1));
            IntPtr pointer;Check(MFCreateMediaType(out pointer));using(var output=new TypeInfo(pointer)) {output.Set(Major,AudioMajor);output.Set(Subtype,Pcm);output.Set(Bits,16u);Check(reader.SetCurrentMediaType(Audio,IntPtr.Zero,output.Pointer));}
            Check(reader.GetCurrentMediaType(Audio,out pointer));int rate,channels,bits,align;
            using(var format=new TypeInfo(pointer)) {if(format.Id(Subtype)!=Pcm) throw new IOException("Windows did not supply PCM audio.");rate=(int)format.Number(Rate);channels=(int)format.Number(Channels);bits=(int)format.Number(Bits);align=(int)format.Number(Align);}
            if(channels<1 || channels>2 || bits!=16 || rate<8000 || rate>192000 || align!=channels*2) throw new IOException("Use mono or stereo audio, 8–192 kHz.");
            var chunks=new List<float[]>();long total=0;
            var converter=new DesktopPcmConverter(rate,channels,16,1,align,channels==2 ? 3u : 0u,(pcm,count)=> {
                total+=count;if(total>Wav.Rate*1800L) throw new IOException("Use source sounds up to 30 minutes long.");var block=new float[count*2];for(int i=0;i<block.Length;i++) block[i]=pcm[i]/32768f;chunks.Add(block);
            });
            int empty=0;byte[] bytes=new byte[8192];
            while(true) {
                uint actual,flags;long timestamp;IntPtr samplePointer;Check(reader.ReadSample(Audio,0,out actual,out flags,out timestamp,out samplePointer));
                if((flags&1)!=0) throw new IOException("Windows reported an error while decoding the audio stream.");
                if(samplePointer!=IntPtr.Zero) {
                    Sample sample=null;MediaBuffer buffer=null;bool locked=false;
                    try {
                        sample=(Sample)Marshal.GetTypedObjectForIUnknown(samplePointer,typeof(Sample));Check(sample.ConvertToContiguousBuffer(out buffer));IntPtr data;uint maximum,length;Check(buffer.Lock(out data,out maximum,out length));locked=true;
                        if(length%align!=0 || length>16*1024*1024) throw new IOException("Invalid decoded audio packet.");
                        if(bytes.Length<length) bytes=new byte[length];Marshal.Copy(data,bytes,0,(int)length);converter.Push(bytes,(int)length/align);empty=0;
                    } finally {if(locked) buffer.Unlock();Release(buffer);Release(sample);Marshal.Release(samplePointer);}
                } else if(++empty>10000) throw new IOException("The audio stream did not produce playable samples.");
                if((flags&2)!=0) break;
            }
            converter.Flush();if(total==0) throw new IOException("This file contains no playable audio.");var samples=new float[checked((int)total*2)];int offset=0;foreach(var block in chunks) {Array.Copy(block,0,samples,offset,block.Length);offset+=block.Length;}
            return new AudioClip(Path.GetFileNameWithoutExtension(path),samples);
        } catch(COMException error) {throw new IOException("Windows could not decode "+Path.GetFileName(path)+". Choose a valid AAC/M4A, MP4 audio, MP3, or WAV file. "+error.Message,error);}
        catch(DllNotFoundException error) {throw new IOException("Windows Media Foundation is unavailable. Export this sound as a PCM WAV in your preparation tool.",error);}
        finally {Release(reader);if(started) MFShutdown();if(com) CoUninitialize();}
    }
}
}

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace TCKAudio {
public sealed class PcmRecorder : IDisposable {
    readonly object gate = new object();
    BlockingCollection<byte[]> queue;
    Thread worker;
    volatile bool active;
    string target, temporary;
    long frames;
    public volatile string Error;
    public bool IsRecording { get { return active; } }
    public long Frames { get { return Interlocked.Read(ref frames); } }
    public static string TakesFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "TCK03", "Takes"); } }

    public void Start(string path) {
        lock (gate) {
            if (queue != null) throw new InvalidOperationException("Finish the current take first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            target = path; temporary = path + ".recording";
            if (File.Exists(path)) throw new IOException("This take already exists. Choose a new name.");
            var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            queue = new BlockingCollection<byte[]>(512); frames = 0; Error = null; active = true;
            worker = new Thread(delegate() { Write(stream); });
            worker.IsBackground = true; worker.Name = "TCKO2 take writer"; worker.Start();
        }
    }

    public void Append(short[] samples, int count) {
        if (!active) return;
        lock (gate) {
            if (!active) return;
            if (Frames + count > 44100L * 1800) { Error = "30-minute take limit reached. Finish this take and start another."; active = false; queue.CompleteAdding(); return; }
            byte[] bytes = new byte[count * 4]; Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            if (!queue.TryAdd(bytes)) { Error = "Disk recording could not keep up. The partial take is retained."; active = false; queue.CompleteAdding(); return; }
            Interlocked.Add(ref frames, count);
        }
    }

    void Write(FileStream stream) {
        long written = 0;
        try {
            using (var writer = new BinaryWriter(stream)) {
                Header(writer, 0);
                try { foreach (byte[] bytes in queue.GetConsumingEnumerable()) { writer.Write(bytes); written += bytes.Length; } }
                finally { stream.Position = 0; Header(writer, written); writer.Flush(); }
            }
        } catch (Exception error) { Error = "Recording failed: " + error.Message; active = false; }
    }

    static void Header(BinaryWriter writer, long bytes) {
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write((uint)(bytes + 36));
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)2);
        writer.Write(44100); writer.Write(176400); writer.Write((ushort)4); writer.Write((ushort)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write((uint)bytes);
    }

    public string Stop() {
        Thread ending;
        lock (gate) { if (queue == null) return null; active = false; queue.CompleteAdding(); ending = worker; }
        ending.Join();
        lock (gate) {
            queue.Dispose(); queue = null; worker = null;
            if (Error != null) throw new IOException(Error + " Recovery file: " + temporary);
            File.Move(temporary, target); return target;
        }
    }
    public void Dispose() { try { Stop(); } catch {} }
}
}

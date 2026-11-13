#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACAI2.Rynth.Threads.Core
{
    public sealed class ThreadGemSessionState
    {
        public int Schema { get; set; } = 1;
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public string CurrentRollId { get; set; } = "";
        public string ActiveRunId { get; set; } = "";
        public List<ThreadGemImprint> Imprints { get; set; } = new List<ThreadGemImprint>();
        public List<ThreadFragmentContext> Fragments { get; set; } = new List<ThreadFragmentContext>();
        public List<ThreadComponentClaim> Claims { get; set; } = new List<ThreadComponentClaim>();
    }
    // Compact index + one file per roll/run. No ever-growing AI summary or all-chat transcript.
    public sealed class ThreadGemStore
    {
        private readonly string folder, server, character;
        private readonly HashSet<string> recoveredPaths = new HashSet<string>();
        private readonly Func<object, string> serialize;
        private readonly Func<string, Type, object> deserialize;
        public List<string> LoadWarnings { get; } = new List<string>();
        public ThreadGemStore(string folder, string server, string character, Func<object, string> serialize, Func<string, Type, object> deserialize)
        { this.folder = folder; this.server = server; this.character = character; this.serialize = serialize; this.deserialize = deserialize; }
        private bool Identity(string s, string c) => s == server && c == character;
        public ThreadGemJournal Load()
        {
            var data = new ThreadGemJournal { Server = server, Character = character };
            var state = Read<ThreadGemSessionState>(Path.Combine(folder, "capture.json"));
            if (state != null)
            {
                if (state.Schema != 1 || !Identity(state.Server, state.Character)) throw new InvalidDataException("Thread Gem capture identity/schema does not match this character.");
                data.Imprints = state.Imprints ?? new List<ThreadGemImprint>();
                data.CurrentRollId = state.CurrentRollId; data.ActiveRunId = state.ActiveRunId; data.Claims = state.Claims ?? new List<ThreadComponentClaim>(); data.Fragments = state.Fragments ?? new List<ThreadFragmentContext>();
            }
            else if (File.Exists(Path.Combine(folder, "capture.json"))) throw new InvalidDataException("Thread Gem capture state is unreadable. It was not overwritten.");
            string rolls = Path.Combine(folder, "rolls");
            if (Directory.Exists(rolls)) foreach (string path in Directory.GetFiles(rolls, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(2000))
            {
                var roll = Read<ThreadGemRoll>(path);
                if (roll != null && Identity(roll.Server, roll.Character) && ValidId(roll.Id) && Path.GetFileNameWithoutExtension(path) == roll.Id) data.Rolls.Add(roll);
                else LoadWarnings.Add("Skipped invalid roll " + Path.GetFileName(path));
            }
            data.Rolls = data.Rolls.OrderBy(x => x.StartedUtc).ThenBy(x => x.Id).ToList();
            string runs = Path.Combine(folder, "runs");
            if (Directory.Exists(runs)) foreach (string path in Directory.GetFiles(runs, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(1000))
            {
                var run = Read<ThreadRunRecord>(path);
                if (run != null && Identity(run.Server, run.Character) && ValidId(run.Id) && Path.GetFileNameWithoutExtension(path) == run.Id) data.Runs.Add(run);
                else LoadWarnings.Add("Skipped invalid run " + Path.GetFileName(path));
            }
            data.Runs = data.Runs.OrderBy(x => x.OpenedUtc).ToList();
            return data;
        }
        public void Save(ThreadGemJournal data, params ThreadGemRoll[] changedRolls)
        {
            if (!Identity(data.Server, data.Character)) throw new InvalidDataException("Wrong Thread Gem identity.");
            // Write records before the index so a crash cannot point to an uncreated record.
            foreach (var roll in changedRolls.Where(x => x != null).GroupBy(x => x.Id).Select(x => x.First())) SaveRoll(roll);
            var run = data.Runs.FirstOrDefault(x => x.Id == data.ActiveRunId);
            if (run != null) SaveRun(run);
            Atomic(Path.Combine(folder, "capture.json"), new ThreadGemSessionState { Server = server, Character = character, CurrentRollId = data.CurrentRollId, ActiveRunId = data.ActiveRunId, Claims = data.Claims, Imprints = data.Imprints, Fragments = data.Fragments });
        }
        public void SaveRoll(ThreadGemRoll roll)
        {
            if (!ValidId(roll.Id) || !Identity(roll.Server, roll.Character)) throw new InvalidDataException("Invalid roll identity.");
            Atomic(Path.Combine(folder, "rolls", roll.Id + ".json"), roll);
        }
        public void SaveRun(ThreadRunRecord run)
        {
            if (!ValidId(run.Id) || !Identity(run.Server, run.Character)) throw new InvalidDataException("Invalid run identity.");
            Atomic(Path.Combine(folder, "runs", run.Id + ".json"), run);
        }
        public void DiscardUnenteredRun(ThreadRunRecord run)
        {
            if (!ValidId(run.Id) || !Identity(run.Server, run.Character) || run.EnteredUtc != null || !string.IsNullOrEmpty(run.RealmInstance))
                throw new InvalidDataException("Only an unentered pending run can be merged.");
            string path = Path.Combine(folder, "runs", run.Id + ".json");
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
        private T Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            if(new FileInfo(path).Length>512*1024){LoadWarnings.Add("Oversized record skipped: "+Path.GetFileName(path));return null;}
            try { return deserialize(File.ReadAllText(path), typeof(T)) as T; }
            catch
            {
                try { var recovered = deserialize(File.ReadAllText(path + ".bak"), typeof(T)) as T; LoadWarnings.Add("Recovered backup for " + Path.GetFileName(path)); recoveredPaths.Add(path); return recovered; }
                catch { LoadWarnings.Add("Unreadable file " + Path.GetFileName(path)); return null; }
            }
        }
        private void Atomic(string path, object value)
        {
            string json=serialize(value);
            if(File.Exists(path)&&File.ReadAllText(path)==json)return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { byte[] bytes = new UTF8Encoding(false).GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temp, path, recoveredPaths.Contains(path) ? null : path + ".bak"); else File.Move(temp, path);
                recoveredPaths.Remove(path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static bool ValidId(string id) { Guid parsed; return Guid.TryParseExact(id, "N", out parsed); }
    }
}

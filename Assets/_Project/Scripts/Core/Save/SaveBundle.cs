using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Core
{
    // Saves packed into one file, to carry them off a device and back (Docs/Saving.md, 내보내기와 가져오기): a
    // character with its maps, a world, or every save on the device. "PTSB" and the format version, the number of
    // saves, then each save's key (SaveKeys; length-prefixed UTF-8) and its data (a length, then the bytes as the
    // device keeps them).
    public static class SaveBundle
    {
        const string Magic = "PTSB";
        const int FormatVersion = 1;
        // Far beyond any real bundle; a damaged or foreign file trips these before it costs much.
        const int MaxSaves = 4096;
        const int MaxKeyBytes = 256;

        public static byte[] Write(IEnumerable<(string Key, byte[] Data)> saves)
        {
            var list = new List<(string Key, byte[] Data)>(saves);
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, new UTF8Encoding(false)))
            {
                w.Write(Encoding.ASCII.GetBytes(Magic));
                w.Write(FormatVersion);
                w.Write(list.Count);
                foreach (var (key, data) in list)
                {
                    if (!SaveKeys.TryParse(key, out _, out _)) throw new ArgumentException($"Not a save key: {key}");
                    if (data == null || data.Length == 0) throw new ArgumentException($"No data for {key}");
                    w.Write(key);
                    w.Write(data.Length);
                    w.Write(data);
                }
            }
            return stream.ToArray();
        }

        // The saves in a bundle, every one checked to be a key of a known kind whose data begins as a save of that
        // kind does, and no key twice. Anything else throws InvalidDataException with a reason a player can read
        // (the menu shows it): the file is not a bundle, is cut short, is from a newer game, or holds something else.
        // Whether each save then reads is up to its own reader, as with any save on the device.
        public static List<(string Key, byte[] Data)> Read(byte[] file)
        {
            if (file == null || file.Length < Magic.Length || Encoding.ASCII.GetString(file, 0, Magic.Length) != Magic)
                throw new InvalidDataException("pray-to 저장 파일이 아닙니다.");
            var saves = new List<(string Key, byte[] Data)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                using var stream = new MemoryStream(file);
                using var r = new BinaryReader(stream, new UTF8Encoding(false));
                r.ReadBytes(Magic.Length);
                int version = r.ReadInt32();
                if (version > FormatVersion) throw new InvalidDataException($"더 새 게임에서 만든 저장 파일입니다(형식 {version}).");
                if (version < 1) throw new InvalidDataException("저장 파일이 깨졌습니다.");
                int count = r.ReadInt32();
                if (count < 0 || count > MaxSaves) throw new InvalidDataException("저장 파일이 깨졌습니다.");
                for (int i = 0; i < count; i++)
                {
                    string key = r.ReadString();
                    if (Encoding.UTF8.GetByteCount(key) > MaxKeyBytes || !SaveKeys.TryParse(key, out var kind, out _))
                        throw new InvalidDataException("저장 파일에 모르는 것이 들어 있습니다.");
                    if (!seen.Add(key)) throw new InvalidDataException("저장 파일에 같은 저장본이 두 번 들어 있습니다.");
                    int length = r.ReadInt32();
                    if (length <= 0 || length > stream.Length - stream.Position) throw new InvalidDataException("저장 파일이 중간에 끊겼습니다.");
                    byte[] data = r.ReadBytes(length);
                    string magic = SaveKeys.MagicOf(kind);
                    if (data.Length < magic.Length || Encoding.ASCII.GetString(data, 0, magic.Length) != magic)
                        throw new InvalidDataException($"저장 파일 속 {Describe(key)} 저장본이 깨졌습니다.");
                    saves.Add((key, data));
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("저장 파일이 깨졌습니다.");
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("저장 파일이 중간에 끊겼습니다.");
            }
            // A key's length that is no length at all.
            catch (FormatException)
            {
                throw new InvalidDataException("저장 파일이 깨졌습니다.");
            }
            return saves;
        }

        // The saves of under with those of over laid on top (Docs/Saving.md, 클라우드 저장): over's every save, then the
        // ones of under whose key over does not have. Uploading a device's saves over the cloud's bundle so keeps what
        // another device put there.
        public static List<(string Key, byte[] Data)> Over(IEnumerable<(string Key, byte[] Data)> under, IEnumerable<(string Key, byte[] Data)> over)
        {
            var saves = new List<(string Key, byte[] Data)>(over);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var save in saves) keys.Add(save.Key);
            foreach (var save in under)
            {
                if (keys.Add(save.Key)) saves.Add(save);
            }
            return saves;
        }

        // "캐릭터 '이름'", "월드 '이름'" or "'이름'의 지도", for messages.
        public static string Describe(string key)
        {
            if (!SaveKeys.TryParse(key, out var kind, out string name)) return key;
            return kind switch
            {
                SaveKind.Character => $"캐릭터 '{name}'",
                SaveKind.World => $"월드 '{name}'",
                _ => $"'{name}'의 지도",
            };
        }
    }

    // A cloud save account (Docs/Saving.md, 클라우드 저장): a name and a password, by the rules of Unity
    // Authentication's username and password sign-in, checked before asking it.
    public static class CloudAccount
    {
        public const int NameMin = 3;
        public const int NameMost = 20;
        public const int PasswordMin = 8;
        public const int PasswordMost = 30;

        // Null when the name and password could be an account, else what to change, for the screen.
        public static string Check(string name, string password)
        {
            name ??= "";
            password ??= "";
            if (name.Length < NameMin || name.Length > NameMost) return $"아이디는 {NameMin}~{NameMost}자로 쓴다.";
            foreach (char c in name)
            {
                bool plain = c < 128 && (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '@' || c == '_');
                if (!plain) return "아이디에는 영문, 숫자와 . - @ _ 만 쓴다.";
            }
            if (password.Length < PasswordMin || password.Length > PasswordMost) return $"비밀번호는 {PasswordMin}~{PasswordMost}자로 쓴다.";
            bool upper = false, lower = false, digit = false, symbol = false;
            foreach (char c in password)
            {
                if (c >= 'A' && c <= 'Z') upper = true;
                else if (c >= 'a' && c <= 'z') lower = true;
                else if (c >= '0' && c <= '9') digit = true;
                else if (c > ' ' && c < 127) symbol = true;
                else return "비밀번호에는 영문, 숫자, 기호만 쓴다.";
            }
            if (!(upper && lower && digit && symbol)) return "비밀번호에는 영문 대문자, 소문자, 숫자, 기호가 하나씩은 있어야 한다.";
            return null;
        }
    }
}

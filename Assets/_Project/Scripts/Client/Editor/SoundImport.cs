using UnityEditor;
using UnityEngine;

namespace Game.Client.Editor
{
    // How the recorded sounds in Resources are imported (Docs/SoundCredits.md, 2026-11-26), so nobody sets it by hand
    // and the web demo stays small: everything mono at 22050 Hz and compressed. The effects (Sfx) are short and
    // decompressed when loaded, to play at once; the music and the ambience (Music, Ambience) are long loops, kept
    // compressed in memory and decoded as they play, at a lower quality. Runs on every import of a file there, also in
    // the web demo's batch build, which imports the project fresh.
    public class SoundImport : AssetPostprocessor
    {
        const string Root = "Assets/_Project/Resources/";

        void OnPreprocessAudio()
        {
            bool effect = assetPath.StartsWith(Root + "Sfx/");
            bool loop = assetPath.StartsWith(Root + "Music/") || assetPath.StartsWith(Root + "Ambience/");
            if (!effect && !loop) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = loop;
            var settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 22050;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = loop ? 0.35f : 0.6f;
            settings.preloadAudioData = !loop;
            importer.defaultSampleSettings = settings;
        }
    }
}

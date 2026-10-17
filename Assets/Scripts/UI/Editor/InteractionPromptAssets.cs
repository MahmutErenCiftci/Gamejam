using System;
using System.IO;
using System.Text;
using AfetUyanisi.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace AfetUyanisi.UI.Editor
{
    /// <summary>Yalnızca kendi UI assetlerini oluşturur; açık sahneleri kaydetmez veya değiştirmez.</summary>
    [InitializeOnLoad]
    public static class InteractionPromptAssets
    {
        public const string Root = "Assets/Resources/InteractionUI";
        public const string PrefabPath = Root + "/InteractionPromptUI.prefab";
        public const string Turkish = "çğıİöşüÇĞÖŞÜ";

        static InteractionPromptAssets()
        {
            EditorApplication.delayCall += CreateIfMissing;
        }

        private static void CreateIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            try { CreateAssets(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        [MenuItem("Afet Uyanisi/Interaction UI/Create or update assets")]
        public static void CreateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Asset üretmeden önce Play Mode'dan çıkın.");

            // TMP, UGUI paketinde zaten kurulu; yalnızca paketle gelen gerekli kaynaklar import edilir.
            if (Shader.Find("TextMeshPro/Mobile/Bitmap") == null)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly);
                UnityEditor.AssetPackage.Package.Import(Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage"), false);
                EditorApplication.delayCall += CreateIfMissing;
                return; // Paket importu bitmeden TMP_Settings/şaderler hazır olmayabilir.
            }
            if (TMP_Settings.LoadDefaultSettings() == null)
                throw new InvalidOperationException("TMP Essential Resources importunun bitmesini bekleyip Create or update assets çalıştırın.");
            EnsureFolder(Root);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/InteractionPixelFont.asset");
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/PixelifySans/PixelifySans-Regular.ttf");
                if (source == null) throw new InvalidOperationException("OFL Pixelify Sans fontu bulunamadı.");
                font = TMP_FontAsset.CreateFontAsset(source, 12, 1, GlyphRenderMode.RASTER_HINTED, 512, 512, AtlasPopulationMode.Dynamic, false);
                font.name = "InteractionPixelFont";
                var characters = new StringBuilder();
                for (int i = 32; i <= 126; i++) characters.Append((char)i);
                characters.Append(Turkish).Append("âîûÂÎÛ–—…’“”");
                if (!font.TryAddCharacters(characters.ToString(), out string missing))
                    throw new InvalidOperationException("Fontta eksik karakterler: " + missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.atlasTextures[0].filterMode = FilterMode.Point;
                AssetDatabase.CreateAsset(font, Root + "/InteractionPixelFont.asset");
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(font);
            }
            if (!font.HasCharacters(Turkish)) throw new InvalidOperationException("TMP atlasında Türkçe karakter eksik.");

            var root = new GameObject("InteractionPromptUI");
            try
            {
                root.AddComponent<InteractionPromptUI>().SetFont(font);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Interaction UI] Prefab hazır: " + PrefabPath + "; Türkçe glif kontrolü PASS. Mevcut TempInteractionHint otomatik kullanır.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

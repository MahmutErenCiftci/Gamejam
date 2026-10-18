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
        public const string FontAssetPath = Root + "/InteractionPixelFont.asset";
        public const string SourceFontPath = "Assets/Fonts/PixelOperator/PixelOperatorTR-Bold.ttf";
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
            // Pixel Operator TR Bold (CC0 + Türkçe harfler, bkz. Assets/Fonts/PixelOperator/README.txt).
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null) throw new InvalidOperationException("Pixel Operator TR Bold fontu bulunamadı: " + SourceFontPath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null) font = CreateFontAsset(source, FontAssetPath);
            else if (!IsBuiltFrom(font, SourceFontPath)) font = ReplaceKeepingGuid(source);
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

        // em = 16 piksel: atlas tam bu boyda ve rasterle örneklenir; Point filtreyle kenarlar yumuşamaz.
        private static TMP_FontAsset CreateFontAsset(Font source, string path)
        {
            var font = TMP_FontAsset.CreateFontAsset(source, 16, 1, GlyphRenderMode.RASTER_HINTED, 512, 512, AtlasPopulationMode.Dynamic, false);
            font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            var characters = new StringBuilder();
            for (int i = 32; i <= 126; i++) characters.Append((char)i);
            characters.Append(Turkish).Append("âîûÂÎÛ–—…’“”→←");
            if (!font.TryAddCharacters(characters.ToString(), out string missing))
                throw new InvalidOperationException("Fontta eksik karakterler: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.atlasTextures[0].filterMode = FilterMode.Point;
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            return font;
        }

        // Statik atlasta sourceFontFile boşaltılır; kaynağı kayıtlı GUID'den okunur.
        private static bool IsBuiltFrom(TMP_FontAsset font, string sourcePath)
        {
            var guid = new SerializedObject(font).FindProperty("m_SourceFontFileGUID");
            return guid != null && guid.stringValue == AssetDatabase.AssetPathToGUID(sourcePath)
                && font.name == Path.GetFileNameWithoutExtension(FontAssetPath);
        }

        // Kaynak font değiştiyse asset dosyası yeniden üretilir ama .meta (GUID) korunur: sahnelerdeki referanslar kırılmaz.
        private static TMP_FontAsset ReplaceKeepingGuid(Font source)
        {
            string tempFolder = Root + "/_rebuild";
            EnsureFolder(tempFolder);
            string temp = tempFolder + "/" + Path.GetFileName(FontAssetPath);
            CreateFontAsset(source, temp);
            File.Copy(temp, FontAssetPath, true);
            AssetDatabase.DeleteAsset(tempFolder);
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
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

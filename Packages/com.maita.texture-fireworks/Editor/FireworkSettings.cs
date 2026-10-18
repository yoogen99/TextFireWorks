using TMPro;
using UnityEngine;

namespace TextureFireWorks.Editor
{
    [CreateAssetMenu(menuName = "TextureFireWorks/花火の設定", fileName = "FireworkSettings")]
    public class FireworkSettings : ScriptableObject
    {
        [TextArea] public string message = "ありがとう！";
        public TMP_FontAsset font;
        [Min(0.1f)] public float characterSize = 1.8f;
        [Range(0f, 100f)] public float lineSpacing = 15f;
        [Range(0.1f, 1f)] public float density = 0.45f;
        [Range(100, 8000)] public int maxPoints = 8000;
        [ColorUsage(true, true)] public Color color = new Color(1f, 0.55f, 0.12f, 1f);
        [Min(0.01f)] public float sparkSize = 0.065f;
        [Min(1f)] public float launchHeight = 15f;
        [Min(0.1f)] public float ascentDuration = 2f;
        [Min(0.1f)] public float spreadDuration = 1.2f;
        [Min(0f)] public float holdDuration = 2f;
        [Min(0.1f)] public float fadeDuration = 2f;
        [Min(0f)] public float gravity = 1.5f;
        public AudioClip ascentClip;
        public AudioClip burstClip;

        public float Duration => ascentDuration + spreadDuration + holdDuration + fadeDuration;
        public Vector4 Durations => new Vector4(ascentDuration, spreadDuration, holdDuration, fadeDuration);

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(message)) throw new System.ArgumentException("文章を入力してください。");
            if (font == null) throw new System.ArgumentException("フォントを指定してください。");
            if (message.Length > 512) throw new System.ArgumentException("文章は512文字以内にしてください。長い文章は発射台を分けてください。");
            if (characterSize <= 0 || density < 0.1f || density > 1 || sparkSize <= 0 || launchHeight < 1 ||
                ascentDuration < 0.1f || spreadDuration < 0.1f || holdDuration < 0 || fadeDuration < 0.1f ||
                gravity < 0 || maxPoints < 1 || maxPoints > 8000)
                throw new System.ArgumentException("サイズ・密度・時間・点数上限が設定範囲外です。");
            if (message.Contains("\t")) throw new System.ArgumentException("タブはスペースに置き換えてください。");
            for (int i = 0; i < message.Length; i++)
            {
                char c = message[i];
                if (char.IsWhiteSpace(c)) continue;
                if (char.IsSurrogate(c)) throw new System.ArgumentException("絵文字などの補助文字は初版では使用できません。");
                // Only the selected font is baked. Do not silently substitute fallback glyphs.
                if (!font.HasCharacter(c, false, true))
                    throw new System.ArgumentException("指定フォントに「" + c + "」がありません。対応するフォントを選んでください。");
            }
        }
    }
}

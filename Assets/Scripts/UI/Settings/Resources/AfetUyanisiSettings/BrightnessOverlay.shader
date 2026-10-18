// Ekran parlaklığı katmanı (GameSettingsRuntime): en üstteki tam ekran UI görüntüsü, altındaki görüntüyü kendi rengiyle
// çarpar. Karartma: Blend DstColor Zero (sonuç = alttaki × renk). Aydınlatma: Blend DstColor One (sonuç = alttaki × (1 + renk)).
// Blend çarpanları malzemeden (_SrcBlend/_DstBlend) seçilir; renk Image.color'dan (köşe rengi) gelir.
Shader "AfetUyanisi/UI/BrightnessOverlay"
{
    Properties
    {
        // Image doku atar; kullanılmaz ama uGUI'nin beklediği özellik.
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
}

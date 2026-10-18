// Deprem yönlendirmesi (EarthquakeGuidance): sprite'ın şeklini tek renk (köşe yumuşatmasız) çizer.
// Etkileşilebilir eşyanın 1 piksel kaydırılmış kopyaları bu shader'la eşyanın ARKASINDA çizilir → piksel kontur / parıltı.
// Işıktan etkilenmez (URP 2D: LightMode etiketi yok = SRPDefaultUnlit). Resources klasöründe: build'e her zaman girer.
Shader "AfetUyanisi/EqGuidanceSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cutoff ("Alfa eşiği", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a;
                clip(a - _Cutoff);
                return fixed4(i.color.rgb, i.color.a);
            }
            ENDCG
        }
    }
}

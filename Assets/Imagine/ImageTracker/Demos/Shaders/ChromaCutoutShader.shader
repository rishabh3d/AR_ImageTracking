Shader "Imagine/ChromaKeyCutout" {
Properties {
    _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
    _MaskCol ("Mask Color", Color)  = (1.0, 0.0, 0.0, 1.0)
    _Sensitivity ("Threshold Sensitivity", Range(0,1)) = 0.5
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    _Feather ("Feathering", Range(0,1)) = 1
}
SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    LOD 100

    Lighting Off

    Pass {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                // Pass precomputed mask CbCr + thresholds from vertex shader
                // so the fragment shader doesn't recalculate per-pixel.
                half4 chromaParams : TEXCOORD2; // xy = mask CbCr, z = S², w = 1/(F²-S²)
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;
            fixed _Feather;

            float4 _MaskCol;
            float _Sensitivity;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                // Precompute mask color CbCr ONCE per vertex instead of per pixel.
                // For a quad (4 vertices) this runs 4 times vs. millions of times per pixel.
                half MCr = 0.5 + 0.5*_MaskCol.r - 0.418688*_MaskCol.g - 0.081312*_MaskCol.b;
                half MCb = 0.5 + (-0.168736)*_MaskCol.r - 0.331264*_MaskCol.g + 0.5*_MaskCol.b;
                half S2 = _Sensitivity * _Sensitivity;
                half F2 = _Feather * _Feather;
                // Precompute reciprocal to avoid division in fragment shader
                half invRange = (F2 > S2) ? (1.0 / (F2 - S2)) : 100000.0;
                o.chromaParams = half4(MCr, MCb, S2, invRange);

                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.texcoord);

                // Convert pixel to CbCr (skip Y — we don't need luminance for chroma keying)
                half Cr = 0.5 + 0.5*c.r - 0.418688*c.g - 0.081312*c.b;
                half Cb = 0.5 + (-0.168736)*c.r - 0.331264*c.g + 0.5*c.b;

                // Squared distance in CbCr space from the mask color
                half2 delta = half2(Cr - i.chromaParams.x, Cb - i.chromaParams.y);
                half sqDist = dot(delta, delta);

                // Branchless feathering — no if/else, no GPU warp divergence.
                // saturate clamps to [0,1] which is a free operation on most GPUs.
                half d = saturate((sqDist - i.chromaParams.z) * i.chromaParams.w);

                clip(d - _Cutoff);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
        ENDCG
    }
}

}
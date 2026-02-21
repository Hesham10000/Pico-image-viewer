Shader "PicoImageViewer/CurvedImage"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Curvature ("Curvature", Range(0, 1)) = 0

        // UI Stencil / Masking support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Curvature;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 localPos = v.vertex;

                // Apply cylindrical curvature: bend the quad along its local X axis.
                // At curvature = 0, the quad is flat. At curvature = 1, it bends into
                // a half-cylinder. The bend pivots around the center of the quad.
                if (_Curvature > 0.001)
                {
                    // Normalize X position to [-1, 1] range based on the mesh bounds.
                    // For a standard UI RectTransform, vertex X is in canvas units.
                    // We use the UV.x to get a normalized [0,1] position across the width.
                    float t = v.uv.x * 2.0 - 1.0; // -1 to +1

                    // Curvature angle: 0 = flat, 1 = 180-degree bend (half cylinder)
                    float angle = t * _Curvature * 3.14159265;

                    // Push the vertex forward (Z) based on its curved position.
                    // cos(angle) pulls center forward, sin(angle) shifts X position.
                    float radius = localPos.x / (t + 0.0001);
                    if (abs(t) < 0.001)
                        radius = 1.0; // avoid division artifacts at center

                    // Simple approach: offset Z based on X position
                    float zOffset = (1.0 - cos(angle)) * abs(localPos.x) * 0.5;
                    localPos.z -= zOffset;

                    // Slightly adjust X to follow the arc
                    float xScale = sin(angle) / (angle + 0.0001);
                    if (abs(angle) < 0.001) xScale = 1.0;
                    localPos.x *= lerp(1.0, xScale, _Curvature);
                }

                o.worldPosition = localPos;
                o.vertex = UnityObjectToClipPos(localPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // UI clipping support
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);

                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}

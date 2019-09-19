Shader "Unlit/SDF Transparent Colored"
{
	Properties
	{
		_MainTex ("Base (RGB), Alpha (A)", 2D) = "black" {}
		_MainColor ("Main Color", Color) = (0, 0, 0, 1)

		_Antialias ("Anti Alias", Range(0, 0.5)) = 0.001

		[Toggle(OUTLINE)]
		_Outline ("Outline", Float) = 0
		_OutlineWidth ("Outline Width", Range(0, 0.5)) = 0.01
		_OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)

		[Toggle(SHADOW)]
		_Shadow ("Shadow", Float) = 0
		_ShadowOffsetH ("Shadow Horizontal Offset", Range(-0.5, 0.5)) = -0.01
		_ShadowOffsetV("Shadow Vertical Offset", Range(-0.5, 0.5)) = 0.01
		_ShadowColor ("Shadow Color", Color) = (1, 0, 0, 1)

		[Toggle(OUTTER_GLOW)]
		_OutterGlow ("Outter Glow", Float) = 0
		_OutterGlowWidth ("Outter Glow Width", Range(0, 0.5)) = 0.05
		_OutterGlowColor ("Outter Glow Color", Color) = (1, 0, 0, 1)
		_OutterGlowIntensity ("Outter Glow Intensity", Range(0, 20)) = 1

		[Toggle(INNER_GLOW)]
		_InnerGlow ("Inner Glow", Float) = 0
		_InnerGlowWidth ("Inner Glow Width", Range(0, 0.5)) = 0.05
		_InnerGlowColor ("Inner Glow Color", Color) = (1, 0, 0, 1)
		_InnerGlowIntensity ("Inner Glow Intensity", Range(0, 20)) = 1
	}
	
	SubShader
	{
		LOD 200

		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"DisableBatching" = "True"
		}
		
		Pass
		{
			Cull Off
			Lighting Off
			ZWrite Off
			Fog { Mode Off }
			Offset -1, -1
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag			
			#pragma shader_feature AA
			#pragma shader_feature OUTLINE
			#pragma shader_feature SHADOW
			#pragma shader_feature OUTTER_GLOW
			#pragma shader_feature INNER_GLOW
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _MainColor;
			fixed _Antialias;
#ifdef OUTLINE
			fixed _OutlineWidth;
			fixed4 _OutlineColor;
#endif
#ifdef SHADOW
			fixed _ShadowOffsetV;
			fixed _ShadowOffsetH;
			fixed4 _ShadowColor;
#endif
#ifdef OUTTER_GLOW
			fixed _OutterGlowWidth;
			fixed4 _OutterGlowColor;
			float _OutterGlowIntensity;
#endif
#ifdef INNER_GLOW
			fixed _InnerGlowWidth;
			fixed4 _InnerGlowColor;
			float _InnerGlowIntensity;
#endif
	
			struct appdata_t
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
	
			struct v2f
			{
				float4 vertex : SV_POSITION;
				half2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
				UNITY_VERTEX_OUTPUT_STEREO
			};
	
			v2f o;

			v2f vert (appdata_t v)
			{
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = v.texcoord;
				o.color = v.color;
				return o;
			}
				
			fixed4 frag (v2f IN) : SV_Target
			{
				fixed dist = tex2D(_MainTex, IN.texcoord).r;
				fixed depth = smoothstep(0, 1, (dist - 0.5 + _Antialias) / (_Antialias + _Antialias));
				fixed4 color = _MainColor * depth;
#ifdef OUTLINE
				color = lerp(color, _OutlineColor, step(dist, 0.5) * step(0.5 - _OutlineWidth, dist));
#endif
#ifdef SHADOW
				fixed dist_shadow = tex2D(_MainTex, IN.texcoord + fixed2(_ShadowOffsetH, _ShadowOffsetV)).r;
				fixed4 color_shadow = _ShadowColor * step(0.5, dist_shadow);
				color = lerp(color, color_shadow, step(color.a, 0));
#endif
#ifdef OUTTER_GLOW
				fixed depth_og = smoothstep(0, 1, (dist - 0.5 + _OutterGlowWidth) / _OutterGlowWidth);
				fixed4 color_og = _OutterGlowColor * depth_og * _OutterGlowIntensity;
				color = lerp(color, color_og, step(color.a, 0));
#endif
#ifdef INNER_GLOW
				fixed depth_ig = step(0.5, dist) * smoothstep(0, 1, (0.5 + _InnerGlowWidth - dist) / _InnerGlowWidth);
				fixed4 color_ig = _InnerGlowColor * depth_ig * _InnerGlowIntensity;
				color = lerp(color, color_ig, color_ig.a);
#endif
				half a = fmod(dist * 1000, 10);
				if (a >= 0 && a <= 1) 
				{
					color = fixed4(1, 0, 0, 1);
				}
				return color * IN.color;
			}
			ENDCG
		}
	}

	SubShader
	{
		LOD 100

		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"DisableBatching" = "True"
		}
		
		Pass
		{
			Cull Off
			Lighting Off
			ZWrite Off
			Fog { Mode Off }
			Offset -1, -1
			//ColorMask RGB
			Blend SrcAlpha OneMinusSrcAlpha
			ColorMaterial AmbientAndDiffuse
			
			SetTexture [_MainTex]
			{
				Combine Texture * Primary
			}
		}
	}
}

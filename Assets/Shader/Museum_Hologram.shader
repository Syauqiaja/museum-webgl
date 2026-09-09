Shader "Museum/Hologram" {
	Properties {
		[Header(Color)] _Color ("Tint", Vector) = (0.2,0.8,1,1)
		_RimColor ("Rim Color", Vector) = (0.4,1,1,1)
		_RimPower ("Rim Power", Range(0.1, 8)) = 2.5
		_RimIntensity ("Rim Intensity", Range(0, 8)) = 2
		[Header(Scanlines)] _ScanTiling ("Scanline Tiling", Float) = 40
		_ScanSpeed ("Scanline Speed", Float) = 2
		_ScanStrength ("Scanline Strength", Range(0, 1)) = 0.4
		[Header(Flicker)] _FlickerSpeed ("Flicker Speed", Float) = 8
		_FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.08
		[Header(Alpha)] _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.25
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
}
//--------------------------------------------------------------------------------------
// File: DisplacedAndShaded.hlsl
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//--------------------------------------------------------------------------------------

//--------------------------------------------------------------------------------------
// Textures
//--------------------------------------------------------------------------------------
Texture2D texDiffuse : register(t[0]);
Texture2D texDisplacement : register(t[1]);
Texture2D texNormal : register(t[2]);

//--------------------------------------------------------------------------------------
// Samplers
//--------------------------------------------------------------------------------------
SamplerState samPoint : register(s[0]);
SamplerState samLinear : register(s[1]);


//--------------------------------------------------------------------------------------
// Constant Buffer Variables
//--------------------------------------------------------------------------------------
cbuffer ConstantBuffer : register(b0)
{
	matrix World;
	matrix View;
	matrix Projection;
	//matrix WorldViewProj;
	float4 LightPos;
	float4 Eye;
	float TessellationFactor;
	float Scaling;
	float DisplacementLevel;
	float Dummy;
}


//--------------------------------------------------------------------------------------
// Structures
//--------------------------------------------------------------------------------------
struct VS_CP_INPUT
{
	float3 PosOS : POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 NormOS : NORMAL;
	float3 BinormalOS : BINORMAL;
	float3 TangentOS : TANGENT;
};

struct VS_CP_OUTPUT
{
	float4 PosWS : POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 NormWS : NORMAL;
	float3 BinormalWS : BINORMAL;
	float3 TangentWS : TANGENT;
	float3 LightTS : LIGHTVECTORS;
	float3 ViewTS : VIEWVECTORS;
};

struct HS_CONST_DATA_OUTPUT
{
	float Edges[3] : SV_TessFactor;
	float Inside[1] : SV_InsideTessFactor;
};

struct HS_CP_OUTPUT
{
	float3 PosWS : WORLDPOS;
	float2 TexCoord : TEXCOORD0;
	float3 NormWS : NORMAL;
	float3 LightTS : LIGHTVECTORS;
	float3 ViewTS : VIEWVECTORS;
};

struct DS_OUTPUT
{
	float4 Pos : SV_POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 NormWS : NORMAL;
	float3 LightTS : LIGHTVECTORTS;
	float3 ViewTS : VIEWVECTORS;
};

VS_CP_OUTPUT VS(VS_CP_INPUT input)
{
	VS_CP_OUTPUT output = (VS_CP_OUTPUT)0;
		
	// Compute world space vectors
	float3 PosWS = mul((float3x3) World, input.PosOS);
	float3 NormalWS = mul((float3x3) World, input.NormOS);
	float3 BinormalWS = mul((float3x3) World, input.BinormalOS);
	float3 TangentWS = mul((float3x3) World, input.TangentOS);

	// Normalize vectors;
	NormalWS = normalize(NormalWS);
	BinormalWS = normalize(BinormalWS);
	TangentWS = normalize(TangentWS);

	// Output position normal and texture coordinates
	output.PosWS = float4(PosWS, 1);
	output.NormWS = NormalWS;
	output.BinormalWS = BinormalWS;
	output.TangentWS = TangentWS;
	output.TexCoord = input.TexCoord * 2;

	// Calculate tangent basis
	float3x3 WorldToTangent = float3x3(TangentWS, BinormalWS, NormalWS);
	//WorldToTangent = transpose(WorldToTangent);

	// Calculate tangent space vectors for lighting
	float3 LightWS = LightPos.xyz - PosWS;
	output.LightTS = mul(WorldToTangent, LightWS);
	float3 ViewWS = Eye - PosWS;
	output.ViewTS = mul(WorldToTangent, ViewWS);

	return output;
}


//--------------------------------------------------------------------------------------
// Hull Shader constant function
//--------------------------------------------------------------------------------------
HS_CONST_DATA_OUTPUT ConstHS(InputPatch<VS_CP_OUTPUT, 3> ip, uint PatchID : SV_PrimitiveID)
{
	HS_CONST_DATA_OUTPUT output;

	// Calculating distance adaptive tessellation factor
	float maxTessellationDistance = 40;
	float minTessellationDistance = 10;
	float4 patchPos = float4(
		(ip[0].PosWS.x + ip[1].PosWS.x + ip[2].PosWS.x) / 3.0f,
		(ip[0].PosWS.y + ip[1].PosWS.y + ip[2].PosWS.y) / 3.0f,
		(ip[0].PosWS.z + ip[1].PosWS.z + ip[2].PosWS.z) / 3.0f,
		(ip[0].PosWS.w + ip[1].PosWS.w + ip[2].PosWS.w) / 3.0f);
	float distance = length(Eye - patchPos) - minTessellationDistance;
	if (distance < 0) distance = 0;
	float t = distance / maxTessellationDistance;
	if (t > 1) t = 1;
	float distanceAdaptiveTessFactor = (1 - t) * TessellationFactor + t;

	// Calculating orientation adaptive tessellation factor
	float3x3 WorldToTangent = float3x3(ip[0].TangentWS, ip[0].BinormalWS, ip[0].NormWS);
	float3 NormalTS = mul(WorldToTangent, ip[0].NormWS);
	normalize(NormalTS);
	float orientationAdaptiveTessFactor = (1.0f - abs(dot(normalize(NormalTS), normalize(ip[0].ViewTS)))) * TessellationFactor;

	output.Edges[0] = output.Edges[1] = output.Edges[2] = distanceAdaptiveTessFactor;
	output.Inside[0] = distanceAdaptiveTessFactor;

	return output;
}


//--------------------------------------------------------------------------------------
// Hull Shader
//--------------------------------------------------------------------------------------
[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("ConstHS")]
HS_CP_OUTPUT HS(InputPatch<VS_CP_OUTPUT, 3> p,
	uint i : SV_OutputControlPointID,
	uint PatchID : SV_PrimitiveID)
{
	HS_CP_OUTPUT output;

	// Pass through the position and the normal vectors
	output.PosWS = p[i].PosWS;
	output.TexCoord = p[i].TexCoord;
	output.NormWS = p[i].NormWS;
	output.LightTS = p[i].LightTS;
	output.ViewTS = p[i].ViewTS;

	return output;
}


//--------------------------------------------------------------------------------------
// Domain Shader
//--------------------------------------------------------------------------------------
[domain("tri")]
DS_OUTPUT DS(HS_CONST_DATA_OUTPUT input,
	float3 BaryCoords : SV_DomainLocation,
	const OutputPatch<HS_CP_OUTPUT, 3> TriPatch)
{
	DS_OUTPUT output;

	// Interpolating position
	float3 vWorldPos = BaryCoords.x * TriPatch[0].PosWS +
		BaryCoords.y * TriPatch[1].PosWS +
		BaryCoords.z * TriPatch[2].PosWS;

	// Interpolating normal vector
	float3 vNormal = BaryCoords.x * TriPatch[0].NormWS +
		BaryCoords.y * TriPatch[1].NormWS +
		BaryCoords.z * TriPatch[2].NormWS;
	vNormal = normalize(vNormal);
	output.NormWS = vNormal;

	// Interpolating and outputting texture coordinates
	output.TexCoord = BaryCoords.x * TriPatch[0].TexCoord +
		BaryCoords.y * TriPatch[1].TexCoord +
		BaryCoords.z * TriPatch[2].TexCoord;

	// Interpolating and outputting tangent space View and Light vectors
	output.LightTS = BaryCoords.x * TriPatch[0].LightTS +
		BaryCoords.y * TriPatch[1].LightTS +
		BaryCoords.z * TriPatch[2].LightTS;
	output.ViewTS = BaryCoords.x * TriPatch[0].ViewTS +
		BaryCoords.y * TriPatch[1].ViewTS +
		BaryCoords.z * TriPatch[2].ViewTS;

	// Displacing generated vertices
	float4 texSample = texDisplacement.SampleLevel(samPoint, output.TexCoord, 0) * 2.0 - 1.0;
	vWorldPos += vNormal * texSample.r * Scaling * DisplacementLevel;
	output.Pos = mul(mul(Projection, View), float4(vWorldPos, 1));

	return output;
}


//--------------------------------------------------------------------------------------
// Function:    ComputeIllumination
// 
// Description: Computes phong illumination for the given pixel using its attribute 
//              textures and a light vector.
//--------------------------------------------------------------------------------------
float4 ComputeIllumination(float2 texCoord, float3 vLightTS, float3 vViewTS)
{
	// Sample the normal from the normal map for the given texture sample:
	float3 vNormalTS = normalize(texNormal.Sample(samLinear, texCoord) * 2.0 - 1.0);
	//vNormalTS.y = -vNormalTS.y;

	// Setting base color
	float4 cBaseColor = texDiffuse.Sample(samLinear, texCoord);

	// Compute ambient component
	float ambientPower = 0.1f;
	float4 ambientColor = float4(1, 1, 1, 1);
	float4 cAmbient = ambientColor * ambientPower;

	// Compute diffuse color component:
	float4 cDiffuse = saturate(dot(vNormalTS, vLightTS));

	// Compute the specular component if desired:
	float3 H = normalize(vLightTS + vViewTS);
	float NdotH = max(0, dot(vNormalTS, H));
	float shininess = 20;
	float specularPower = 10;
	float4 specularColor = float4(1, 1, 1, 1);
	float4 cSpecular = pow(saturate(NdotH), shininess) * specularColor * specularPower;

	// Composite the final color:
	float4 cFinalColor = (cAmbient + cDiffuse) * cBaseColor + cSpecular;

	return cFinalColor;
}

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 PS(DS_OUTPUT input) : SV_Target
{
	float3 LightTS = normalize(input.LightTS);
	float3 ViewTS = normalize(input.ViewTS);

	float4 finalColor = ComputeIllumination(input.TexCoord, LightTS, ViewTS);

	return finalColor;
}

//--------------------------------------------------------------------------------------
// Solid Pixel Shader for Wireframe
//--------------------------------------------------------------------------------------
float4 SolidPS(DS_OUTPUT input) : SV_Target
{
	return float4 (1, 1, 1, 1);
}
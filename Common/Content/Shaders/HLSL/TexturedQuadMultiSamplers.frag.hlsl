Texture2D<float4> Texture1 : register(t0, space2);
Texture2D<float4> Texture2 : register(t1, space2);

SamplerState Sampler1 : register(s0, space2);
SamplerState Sampler2 : register(s1, space2);

float4 main(float2 TexCoord : TEXCOORD0) : SV_Target0
{
    return lerp(Texture1.Sample(Sampler1, TexCoord), Texture2.Sample(Sampler2, TexCoord), 0.2);
}

cbuffer UniformBlock : register(b0, space3)
{
    float4 MyColor : packoffset(c0);
};

float4 main(float4 InputColor : TEXCOORD0) : SV_Target0
{
    return MyColor;
}

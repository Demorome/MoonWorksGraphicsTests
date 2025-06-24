using System.Numerics;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;

namespace MoonWorksGraphicsTests;

[StructLayout(LayoutKind.Sequential)]
public struct TransformVertexUniform
{
	public Matrix4x4 ViewProjection;

	public TransformVertexUniform(Matrix4x4 viewProjection)
	{
		ViewProjection = viewProjection;
	}
}

[StructLayout(LayoutKind.Sequential)]
public struct ColorFragmentUniform
{
	public FColor Color;

	public ColorFragmentUniform(FColor color)
	{
		Color = color;
	}
}

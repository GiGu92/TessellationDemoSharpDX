using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buffer11 = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;


namespace SharpHelper
{
	/// <summary>
	/// To Render Static Object
	/// </summary>
	public class SharpMesh : IDisposable
	{

		/// <summary>
		/// Device pointer
		/// </summary>
		public SharpDevice Device { get; private set; }

		/// <summary>
		/// Vertex Buffer
		/// </summary>
		public Buffer11 VertexBuffer { get; private set; }

		/// <summary>
		/// Index Buffer
		/// </summary>
		public Buffer11 IndexBuffer { get; private set; }

		/// <summary>
		/// Vertex Size
		/// </summary>
		public int VertexSize { get; private set; }

		/// <summary>
		/// Mesh Parts
		/// </summary>
		public List<SharpSubSet> SubSets { get; private set; }

		private SharpMesh(SharpDevice device)
		{
			Device = device;
			SubSets = new List<SharpSubSet>();
		}


		/// <summary>
		/// Draw Mesh
		/// </summary>
		public void Draw()
		{
			Device.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
			Device.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, VertexSize, 0));
			Device.DeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
			Device.DeviceContext.DrawIndexed(SubSets[0].IndexCount, 0, 0);
		}


		/// <summary>
		/// Draw Mesh
		/// </summary>
		/// <param name="subset">Subsets</param>
		public void Draw(int subset)
		{
			Device.DeviceContext.DrawIndexed(SubSets[subset].IndexCount, SubSets[subset].StartIndex, 0);
		}

		/// <summary>
		/// Create From Vertices and Indices array
		/// </summary>
		/// <typeparam name="VType">Vertex Type</typeparam>
		/// <param name="device">Device</param>
		/// <param name="vertices">Vertices</param>
		/// <param name="indices">Indices</param>
		/// <returns>Mesh</returns>
		public static SharpMesh Create<VType>(SharpDevice device, VType[] vertices, int[] indices) where VType : struct
		{
			SharpMesh mesh = new SharpMesh(device);
			mesh.VertexBuffer = Buffer11.Create<VType>(device.Device, BindFlags.VertexBuffer, vertices);
			mesh.IndexBuffer = Buffer11.Create(device.Device, BindFlags.IndexBuffer, indices);
			mesh.VertexSize = SharpDX.Utilities.SizeOf<VType>();
			mesh.SubSets.Add(new SharpSubSet()
			{
				DiffuseColor = new Vector4(1, 1, 1, 1),
				IndexCount = indices.Count()
			});
			return mesh;
		}

		/// <summary>
		/// Create a mesh from wavefront obj file format
		/// </summary>
		/// <param name="device">Device</param>
		/// <param name="filename">Filename</param>
		/// <returns>Mesh</returns>
		public static SharpMesh CreateFromObj(SharpDevice device, string filename)
		{
			SharpMesh mesh = new SharpMesh(device);

			WaveFrontModel[] modelParts = WaveFrontModel.CreateFromObj(filename);
			mesh.Device = device;
			mesh.SubSets = new List<SharpSubSet>();

			List<StaticVertex> vertices = new List<StaticVertex>();
			List<int> indices = new List<int>();

			int vcount = 0;
			int icount = 0;
			foreach (WaveFrontModel model in modelParts)
			{
				vertices.AddRange(model.VertexData);
				indices.AddRange(model.IndexData.Select(i => i + vcount));

				var mate = model.MeshMaterial.First();


				ShaderResourceView tex = null;
				if (!string.IsNullOrEmpty(mate.DiffuseMap))
				{
					string textureFile = System.IO.Path.GetDirectoryName(filename) + "\\" + System.IO.Path.GetFileName(mate.DiffuseMap);
					tex = ShaderResourceView.FromFile(device.Device, textureFile);
				}

				mesh.SubSets.Add(new SharpSubSet()
				{
					IndexCount = model.IndexData.Count,
					StartIndex = icount,
					DiffuseMap = tex
				});



				vcount += model.VertexData.Count;
				icount += model.IndexData.Count;
			}

			mesh.VertexBuffer = Buffer11.Create<StaticVertex>(device.Device, BindFlags.VertexBuffer, vertices.ToArray());
			mesh.IndexBuffer = Buffer11.Create(device.Device, BindFlags.IndexBuffer, indices.ToArray());
			mesh.VertexSize = SharpDX.Utilities.SizeOf<StaticVertex>();

			return mesh;
		}

		/// <summary>
		/// Creates vertices array from wavefront obj file format
		/// </summary>
		/// <param name="filename">Filename</param>
		/// <returns>Vertices</returns>
		public static StaticVertex[] CreateVerticesArrayFromObj(string filename)
		{
			WaveFrontModel[] modelParts = WaveFrontModel.CreateFromObj(filename);

			List<StaticVertex> vertices = new List<StaticVertex>();
			foreach (WaveFrontModel model in modelParts)
			{
				vertices.AddRange(model.VertexData);
			}

			return vertices.ToArray();
		}

		/// <summary>
		/// Creates indices array from wavefront obj file format
		/// </summary>
		/// <param name="filename">Filename</param>
		/// <returns>Indices</returns>
		public static int[] CreateIndicesArrayFromObj(string filename)
		{
			WaveFrontModel[] modelParts = WaveFrontModel.CreateFromObj(filename);
			List<int> indices = new List<int>();

			int vcount = 0;
			foreach (WaveFrontModel model in modelParts)
			{
				indices.AddRange(model.IndexData.Select(i => i + vcount));
				vcount += model.VertexData.Count;
			}

			return indices.ToArray();
		}

		public static void CalculateTangents(StaticVertex[] vertices, int[] indices, out Vector3[] tangents, out Vector3[] binormals)
		{
			Vector3[] tan = new Vector3[vertices.Length];
			Vector3[] bin = new Vector3[vertices.Length];

			for (int i = 0; i < indices.Length / 3; i++)
			{
				int i1 = indices[i * 3];
				int i2 = indices[i * 3 + 1];
				int i3 = indices[i * 3 + 2];

				Vector3 v1 = vertices[i1].Position;
				Vector3 v2 = vertices[i2].Position;
				Vector3 v3 = vertices[i3].Position;

				Vector2 w1 = vertices[i1].TextureCoordinate;
				Vector2 w2 = vertices[i2].TextureCoordinate;
				Vector2 w3 = vertices[i3].TextureCoordinate;

				float x1 = v2.X - v1.X;
				float y1 = v2.Y - v1.Y;
				float z1 = v2.Z - v1.Z;
				float x2 = v3.X - v1.X;
				float y2 = v3.Y - v1.Y;
				float z2 = v3.Z - v1.Z;

				float s1 = w2.X - w1.X;
				float t1 = w2.Y - w1.Y;
				float s2 = w3.X - w1.X;
				float t2 = w3.Y - w1.Y;

				float r = 1.0f / (s1 * t2 - s2 * t1);

				Vector3 sdir = new Vector3(
					(t2 * x1 - t1 * x2) * r,
					(t2 * y1 - t1 * y2) * r,
					(t2 * z1 - t1 * z2) * r);

				Vector3 tdir = new Vector3(
					(s1 * x2 - s2 * x1) * r,
					(s1 * y2 - s2 * y1) * r,
					(s1 * z2 - s2 * z1) * r);

				tan[i1] += sdir;
				tan[i2] += sdir;
				tan[i3] += sdir;

				bin[i1] += tdir;
				bin[i2] += tdir;
				bin[i3] += tdir;
			}

			tangents = tan;
			binormals = bin;
		}


		/// <summary>
		/// Create a mesh from wavefront obj file format using Tangent and Binormal vertex format
		/// </summary>
		/// <param name="device">Device</param>
		/// <param name="filename">Filename</param>
		/// <returns>Mesh</returns>
		public static SharpMesh CreateNormalMappedFromObj(SharpDevice device, string filename)
		{
			SharpMesh mesh = new SharpMesh(device);

			WaveFrontModel[] modelParts = WaveFrontModel.CreateFromObj(filename);
			mesh.Device = device;
			mesh.SubSets = new List<SharpSubSet>();

			List<TangentVertex> vertices = new List<TangentVertex>();
			List<int> indices = new List<int>();

			int vcount = 0;
			int icount = 0;
			foreach (WaveFrontModel model in modelParts)
			{
				vertices.AddRange(model.TangentData);
				indices.AddRange(model.IndexData.Select(i => i + vcount));

				var mate = model.MeshMaterial.First();


				ShaderResourceView tex = null;
				ShaderResourceView ntex = null;

				if (!string.IsNullOrEmpty(mate.DiffuseMap))
				{
					string textureFile = Path.GetDirectoryName(filename) + "\\" + Path.GetFileName(mate.DiffuseMap);
					tex = ShaderResourceView.FromFile(device.Device, textureFile);

					string normalMap = Path.GetDirectoryName(textureFile) + "\\" + Path.GetFileNameWithoutExtension(textureFile) + "N" + Path.GetExtension(textureFile);
					ntex = ShaderResourceView.FromFile(device.Device, normalMap);
				}

				mesh.SubSets.Add(new SharpSubSet()
				{
					IndexCount = model.IndexData.Count,
					StartIndex = icount,
					DiffuseMap = tex,
					NormalMap = ntex
				});

				vcount += model.VertexData.Count;
				icount += model.IndexData.Count;
			}

			mesh.VertexBuffer = Buffer11.Create<TangentVertex>(device.Device, BindFlags.VertexBuffer, vertices.ToArray());
			mesh.IndexBuffer = Buffer11.Create(device.Device, BindFlags.IndexBuffer, indices.ToArray());
			mesh.VertexSize = SharpDX.Utilities.SizeOf<TangentVertex>();

			return mesh;
		}

		/// <summary>
		/// Create a quad for Multiple Render Target
		/// </summary>
		/// <param name="device">Device</param>
		/// <returns>Mesh</returns>
		public static SharpMesh CreateQuad(SharpDevice device)
		{
			Vector3[] vertices = new Vector3[] 
			{ 
				new Vector3(-1, 1, 0), 
				new Vector3(-1, -1, 0), 
				new Vector3(1, 1, 0), 
				new Vector3(1, -1, 0) 
			};

			int[] indices = new int[] { 0, 2, 1, 2, 3, 1 };
			SharpMesh mesh = new SharpMesh(device);
			mesh.VertexBuffer = Buffer11.Create<Vector3>(device.Device, BindFlags.VertexBuffer, vertices.ToArray());
			mesh.IndexBuffer = Buffer11.Create(device.Device, BindFlags.IndexBuffer, indices.ToArray());
			mesh.VertexSize = SharpDX.Utilities.SizeOf<Vector3>();

			mesh.SubSets.Add(new SharpSubSet()
			{
				DiffuseColor = new Vector4(1, 1, 1, 1),
				IndexCount = indices.Count()
			});

			return mesh;
		}

		/// <summary>
		/// Set all buffer and topology property to speed up rendering
		/// </summary>
		public void Begin()
		{
			Device.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
			Device.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, VertexSize, 0));
			Device.DeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
		}

		/// <summary>
		/// Draw all vertices as points
		/// </summary>
		/// <param name="count"></param>
		public void DrawPoints(int count = int.MaxValue)
		{
			Device.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
			Device.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, VertexSize, 0));
			Device.DeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
			Device.DeviceContext.DrawIndexed(Math.Min(count, SubSets[0].IndexCount), 0, 0);
		}

		/// <summary>
		/// Draw patch
		/// </summary>
		/// <param name="topology">Patch Topology type</param>
		public void DrawPatch(PrimitiveTopology topology)
		{
			Device.DeviceContext.InputAssembler.PrimitiveTopology = topology;
			Device.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(VertexBuffer, VertexSize, 0));
			Device.DeviceContext.InputAssembler.SetIndexBuffer(IndexBuffer, Format.R32_UInt, 0);
			Device.DeviceContext.DrawIndexed(SubSets[0].IndexCount, 0, 0);
		}

		/// <summary>
		/// Release resource
		/// </summary>
		public void Dispose()
		{
			VertexBuffer.Dispose();
			IndexBuffer.Dispose();
			foreach (var s in SubSets)
			{
				if (s.DiffuseMap != null)
					s.DiffuseMap.Dispose();

				if (s.NormalMap != null)
					s.NormalMap.Dispose();
			}
		}
	}
}

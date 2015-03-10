using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using SharpHelper;
using System;
using System.Windows.Forms;
using Buffer11 = SharpDX.Direct3D11.Buffer;

namespace TessellationDemoSharpDX
{
	static class Program
	{
		//struct used to set shader constant buffer
		struct ConstantBufferData
		{
			public Matrix World;
			public Matrix View;
			public Matrix Projection;
			//public Matrix WorldViewProj;
			public Vector4 LightPos;
			public Vector4 Eye;
			public float TessellationFactor;
			public float Scaling;
			public float DisplacementLevel;
			public float Dummy;
		}

		struct Vertex
		{
			public Vector3 Pos;
			public Vector2 TexCoord;
			public Vector3 Normal;
			public Vector3 Binormal;
			public Vector3 Tangent;
		}

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			if (!SharpDevice.IsDirectX11Supported())
			{
				System.Windows.Forms.MessageBox.Show("DirectX11 Not Supported");
				return;
			}

			//render form
			RenderForm form = new RenderForm();
			form.Text = "Tessellation Demo";

			//frame rate counter
			SharpFPS fpsCounter = new SharpFPS();


			using (SharpDevice device = new SharpDevice(form, true))
			{
				//load font
				SharpBatch font = new SharpBatch(device, "textfont.dds");

				//load vertices and indices array from wavefront obj file
				var staticVertices = SharpMesh.CreateVerticesArrayFromObj("../../../Models/sphere.obj");
				var indices = SharpMesh.CreateIndicesArrayFromObj("../../../Models/sphere.obj");

				//computing tangents and binormals for vertices
				Vector3[] tangents;
				Vector3[] binormals;
				SharpMesh.CalculateTangents(staticVertices, indices, out tangents, out binormals);

				//creating vertices array from staticvertex and tangent information
				Vertex[] vertices = new Vertex[staticVertices.Length];
				for (int i = 0; i < staticVertices.Length; i++)
				{
					vertices[i].Pos = staticVertices[i].Position;
					vertices[i].TexCoord = staticVertices[i].TextureCoordinate;
					vertices[i].Normal = staticVertices[i].Normal;
					vertices[i].Binormal = binormals[i];
					vertices[i].Tangent = tangents[i];
				}

				//creating mesh from vertices and indices
				SharpMesh mesh = SharpMesh.Create<Vertex>(device, vertices, indices);

				//loading textures
				foreach (var subset in mesh.SubSets)
				{
					subset.DiffuseMap = ShaderResourceView.FromFile(device.Device, "../../Textures/white.jpg");
					subset.NormalMap = ShaderResourceView.FromFile(device.Device, "../../Textures/Normal/spikes_normal.jpg");
					subset.DisplacementMap = ShaderResourceView.FromFile(device.Device, "../../Textures/Displacement/spikes_displacement.jpg");
				}

				//creating samplers
				SamplerStateDescription pointSamplerDesc = new SamplerStateDescription()
				{
					Filter = Filter.ComparisonMinMagMipPoint,
					AddressU = TextureAddressMode.Wrap,
					AddressV = TextureAddressMode.Wrap,
					AddressW = TextureAddressMode.Wrap,
					ComparisonFunction = Comparison.Never,
					MinimumLod = 0,
					MaximumLod = float.MaxValue
				};
				SamplerState pointSampler = new SamplerState(device.Device, pointSamplerDesc);

				SamplerStateDescription linearSamplerDesc = new SamplerStateDescription()
				{
					Filter = Filter.ComparisonMinMagMipLinear,
					AddressU = TextureAddressMode.Wrap,
					AddressV = TextureAddressMode.Wrap,
					AddressW = TextureAddressMode.Wrap,
					ComparisonFunction = Comparison.Never,
					MinimumLod = 0,
					MaximumLod = float.MaxValue
				};
				SamplerState linearSampler = new SamplerState(device.Device, linearSamplerDesc);

				//init shader
				string shaderfile = "../../Shaders/DisplacedAndShaded2.hlsl";
				SharpShader shader = new SharpShader(device, shaderfile,
					new SharpShaderDescription()
					{
						VertexShaderFunction = "VS",
						HullShaderFunction = "HS",
						DomainShaderFunction = "DS",
						PixelShaderFunction = "PS"
					},
					new InputElement[] 
					{  
						new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
						new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
						new InputElement("NORMAL", 0, Format.R32G32B32_Float, 20, 0),
						new InputElement("BINORMAL", 0, Format.R32G32_Float, 32, 0),
						new InputElement("TANGENT", 0, Format.R32G32_Float, 44, 0)
					});


				//init constant buffer
				Buffer11 buffer = shader.CreateBuffer<ConstantBufferData>();

				RasterizerStateDescription defaultdesc = RasterizerStateDescription.Default();
				//defaultdesc.CullMode = CullMode.None;

				RasterizerStateDescription wireframedesc = RasterizerStateDescription.Default();
				//wireframedesc.CullMode = CullMode.None;
				wireframedesc.FillMode = FillMode.Wireframe;

				device.SetRasterState(defaultdesc);

				//camera init
				Camera camera = new Camera()
				{
					Eye = new Vector3(0, 15, -30),
					Target = new Vector3(0, 0, 0),
					Up = Vector3.UnitY,
					AspectRatio = (float)form.ClientRectangle.Width / (float)form.ClientRectangle.Height,
					FieldOfView = 3.14f / 3.0f,
					NearClippingPane = 1.0f,
					FarClippingPane = 1000.0f
				};

				// Timer init
				TimerTick timer = new TimerTick();
				timer.Tick();

				//init frame counter
				fpsCounter.Reset();

				bool wireframe = false;
				bool rotating = false;
				float tessFactor = 10.0f;

				//handle KeyDown events
				form.KeyDown += (sender, e) =>
				{
					if (e.KeyCode == Keys.F)
					{
						wireframe = !wireframe;
						if (wireframe)
						{
							device.SetRasterState(wireframedesc);
							var solidPSByteCode = ShaderBytecode.CompileFromFile(shaderfile, "SolidPS", "ps_5_0");
							shader.PixelShader = new PixelShader(device.Device, solidPSByteCode);
						}
						else
						{
							device.SetRasterState(defaultdesc);
							var defaultPSByteCode = ShaderBytecode.CompileFromFile(shaderfile, "PS", "ps_5_0");
							shader.PixelShader = new PixelShader(device.Device, defaultPSByteCode);
						}
					}
					if (e.KeyCode == Keys.R) rotating = !rotating;

					if (e.KeyCode == Keys.Up && tessFactor < 64) tessFactor += 0.5f;
					if (e.KeyCode == Keys.Down && tessFactor > 1) tessFactor -= 0.5f;

					if (e.KeyCode == Keys.W) camera.IsMovingForward = true;
					if (e.KeyCode == Keys.S) camera.IsMovingBackward = true;
					if (e.KeyCode == Keys.A) camera.IsMovingLeft = true;
					if (e.KeyCode == Keys.D) camera.IsMovingRight = true;
					if (e.KeyCode == Keys.E) camera.IsMovingUp = true;
					if (e.KeyCode == Keys.Q) camera.IsMovingDown = true;

					if (e.KeyCode == Keys.Escape) Application.Exit();
				};

				//handle KeyUp events
				form.KeyUp += (sender, e) =>
				{
					if (e.KeyCode == Keys.W) camera.IsMovingForward = false;
					if (e.KeyCode == Keys.S) camera.IsMovingBackward = false;
					if (e.KeyCode == Keys.A) camera.IsMovingLeft = false;
					if (e.KeyCode == Keys.D) camera.IsMovingRight = false;
					if (e.KeyCode == Keys.E) camera.IsMovingUp = false;
					if (e.KeyCode == Keys.Q) camera.IsMovingDown = false;
				};


				float t = 0.0f;
				float rt = 0.0f;

				//main loop
				RenderLoop.Run(form, () =>
				{
					//calculating elapsed time
					float dt = (float)Environment.TickCount - t;
					t = (float)Environment.TickCount;
					if (rotating) rt += dt;

					//Resizing
					if (device.MustResize)
					{
						device.Resize();
						font.Resize();
						camera.AspectRatio = (float)form.ClientRectangle.Width / (float)form.ClientRectangle.Height;
					}

					//apply states
					device.UpdateAllStates();

					//clear color
					device.Clear(Color.CornflowerBlue);

					//apply shader
					shader.Apply();

					//set transformation matrix
					Matrix world =
						Matrix.RotationY((float)Math.PI) *
						Matrix.Scaling(10) *
						Matrix.RotationY(rt / 1000.0F);

					ConstantBufferData cbData = new ConstantBufferData()
					{
						World = world,
						View = camera.View,
						Projection = camera.Projection,
						LightPos = new Vector4(100, 100, 0, 1),
						Eye = new Vector4(camera.Eye.X, camera.Eye.Y, camera.Eye.Z, 1.0f),
						TessellationFactor = tessFactor,
						Scaling = 1,
						DisplacementLevel = 1.0f,
						Dummy = 0
					};

					//write data inside constant buffer
					device.UpdateData<ConstantBufferData>(buffer, cbData);

					//apply constant buffer to shader
					device.DeviceContext.VertexShader.SetConstantBuffer(0, buffer);
					device.DeviceContext.HullShader.SetConstantBuffer(0, buffer);
					device.DeviceContext.DomainShader.SetConstantBuffer(0, buffer);
					device.DeviceContext.PixelShader.SetConstantBuffer(0, buffer);

					//draw mesh as patch
					mesh.Begin();
					for (int i = 0; i < mesh.SubSets.Count; i++)
					{
						device.DeviceContext.DomainShader.SetShaderResource(1, mesh.SubSets[i].DisplacementMap);
						device.DeviceContext.DomainShader.SetSampler(0, linearSampler);

						device.DeviceContext.PixelShader.SetShaderResource(0, mesh.SubSets[i].DiffuseMap);
						device.DeviceContext.PixelShader.SetShaderResource(2, mesh.SubSets[i].NormalMap);
						device.DeviceContext.PixelShader.SetSampler(1, linearSampler);

						mesh.DrawPatch(SharpDX.Direct3D.PrimitiveTopology.PatchListWith3ControlPoints);
					}


					//begin drawing text
					font.Begin();

					//draw string
					fpsCounter.Update();
					font.DrawString("FPS: " + fpsCounter.FPS, 0, 0, Color.Yellow);

					//update camera
					camera.Update(dt);

					//flush text to view
					font.End();
					//present
					device.Present();

				});

				//release resource
				font.Dispose();
				mesh.Dispose();
				buffer.Dispose();
			}
		}
	}
}

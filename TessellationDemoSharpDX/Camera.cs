using SharpDX;

namespace TessellationDemoSharpDX
{
	public class Camera
	{
		public Vector3 Eye { get; set; }
		public Vector3 Target { get; set; }
		public Vector3 Up { get; set; }
		public Vector3 Direction
		{
			get
			{
				Vector3 dir = Target - Eye;
				dir.Normalize();
				return dir;
			}
		}

		public float AspectRatio { get; set; }
		public float FieldOfView { get; set; }
		public float NearClippingPane { get; set; }
		public float FarClippingPane { get; set; }

		public bool IsMovingForward { get; set; }
		public bool IsMovingBackward { get; set; }
		public bool IsMovingLeft { get; set; }
		public bool IsMovingRight { get; set; }
		public bool IsMovingUp { get; set; }
		public bool IsMovingDown { get; set; }

		public float MovingSpeed { get; set; }

		public Matrix View
		{
			get
			{
				return Matrix.LookAtLH(Eye, Target, Up);
			}
		}

		public Matrix Projection
		{
			get
			{
				return Matrix.PerspectiveFovLH(FieldOfView, AspectRatio, NearClippingPane, FarClippingPane);
			}
		}

		public Camera()
		{
			IsMovingForward = false;
			IsMovingBackward = false;
			IsMovingLeft = false;
			IsMovingRight = false;
			IsMovingUp = false;
			IsMovingDown = false;

			MovingSpeed = 0.02f;
		}

		public void Update(float dt)
		{
			if (dt > 0)
			{
				if (IsMovingForward) Eye += Direction * dt * MovingSpeed;
				if (IsMovingBackward) Eye += -Direction * dt * MovingSpeed;
				if (IsMovingLeft)
				{
					Vector3 left = Vector3.Cross(Direction, Up);
					left.Normalize();
					Eye += left * dt * MovingSpeed;
				}
				if (IsMovingRight)
				{
					Vector3 right = -Vector3.Cross(Direction, Up);
					right.Normalize();
					Eye += right * dt * MovingSpeed;
				}
				if (IsMovingUp) Eye += Up * dt * MovingSpeed;
				if (IsMovingDown) Eye += -Up * dt * MovingSpeed;
			}
		}
	}
}
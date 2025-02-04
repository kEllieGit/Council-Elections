using Sandbox;
using Sandbox.Citizen;
using Sandbox.UI;
using Sandbox.Utility;
using System.Diagnostics;
using System.Xml.Linq;
using static Sandbox.PhysicsContact;

public abstract partial class Actor : Component
{
	[Property]
	[Category( "Info" ), Order( 1 )]
	public bool RandomNameOnSpawn { get; set; } = true;

	[Sync( SyncFlags.FromHost )]
	[Property, HideIf( "RandomNameOnSpawn", true )]
	[Category( "Info" ), Order( 1 )]
	public string FirstName { get; set; } = "John";

	[Sync( SyncFlags.FromHost )]
	[Property, HideIf( "RandomNameOnSpawn", true )]
	[Category( "Info" ), Order( 1 )]
	public string LastName { get; set; } = "Doe";

	[Property, HideIf( "RandomNameOnSpawn", true )]
	[Category( "Info" ), Order( 1 )]
	public virtual string UserName { get; set; }

	[Property, HideIf( "RandomNameOnSpawn", true )]
	[Category( "Info" ), Order( 1 )]
	public string FullName => string.IsNullOrWhiteSpace( UserName ) ? $"{FirstName} {LastName}" : UserName;

	[Property]
	[Category( "Info" ), Order( 1 )]
	public Gender Gender { get; set; } = Gender.Undefined;

	[Property, HideIf( "RandomNameOnSpawn", true )]
	[Button( "Random Name", "casino" )]
	[Category( "Info" ), Order( 1 )]
	public void GenerateRandomName()
	{
		var randomName = Actor.RandomName( Gender );
		FirstName = randomName.Item1;
		LastName = randomName.Item2;
	}

	[Property]
	[Category( "Stats" ), Order( 2 )]
	[Range( 0f, 200f, 10f )]
	public float WalkSpeed { get; set; } = 70f;

	[Property]
	[Category( "Stats" ), Order( 2 )]
	[Range( 0f, 400f, 10f )]
	public float RunSpeed { get; set; } = 160f;

	[Property]
	[Category( "Stats" ), Order( 2 )]
	public RangedFloat HeightRange { get; set; } = new RangedFloat( 0.5f, 1.5f );

	[Property]
	[Category( "Stats" ), Order( 2 )]
	public RangedFloat VoicePitch { get; set; } = new RangedFloat( 0.9f, 1.1f );

	public enum Expression
	{
		None,
		Smile,
		Frown,
		Surprise,
		Sad,
		Angry
	}

	[Property]
	[Category( "Stats" ), Order( 2 )]
	public Expression DefaultExpression { get; set; }

	[Property]
	[Category( "Stats" ), Order( 2 )]
	[WideMode]
	public List<string> InteractPhrases { get; set; } = new();

	[Property]
	[Category( "Components" ), Order( 5 )]
	public SkinnedModelRenderer ModelRenderer { get; set; }

	[Property]
	[Category( "Components" ), Order( 5 )]
	public BoxCollider Collider { get; set; }

	[Property]
	[Category( "Components" ), Order( 5 )]
	public NavMeshAgent Agent { get; set; }

	[Property]
	[Category( "Components" ), Order( 5 )]
	public CitizenAnimationHelper AnimationHelper { get; set; }

	[Property]
	[Category( "Components" ), Order( 5 )]
	public Interaction Interaction { get; set; }

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner SkinClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner HairClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner FaceClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner FacialClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner HairbrowsClothing { get; set; } = new(); // LOL I meant eyebrows, too late to go back

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner ShirtClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner HoodieClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner PantsClothing { get; set; } = new();

	[Property]
	[Category( "Clothing" ), Order( 6 )]
	public ClothesPlanner ShoesClothing { get; set; } = new();

	public bool InLine = true;
	public bool Inside = false;
	public bool Voting = false;
	public bool Exiting = false;


	public bool IsRunning { get; set; } = false;
	public float WishSpeed => IsRunning ? RunSpeed : WalkSpeed;

	private ModelPhysics _ragdoll;
	private float _randomSeed;
	[Sync]
	public float Pitch { get; set; } = 1f;

	public GameObject LookingTo { get; set; }
	public bool Talking { get; set; } = false;
	public TimeUntil StopTalking { get; set; }
	public TimeUntil StopLooking { get; set; }

	[Sync]
	public TimeUntil UnragdollTimer { get; set; }
	[Sync]
	public bool Ragdolled { get; set; } = false;

	[Sync]
	public Vector3 Velocity { get; set; }



	protected override void OnStart()
	{
		if ( IsProxy ) return;

		_randomSeed = Game.Random.Float( -10000f, 10000f );

		if ( RandomNameOnSpawn )
			GenerateRandomName();

		if ( Interaction.IsValid() )
			Interaction.InteractionName = FullName;

		if ( AnimationHelper.IsValid() )
			AnimationHelper.Height = Game.Random.Float( HeightRange.Min, HeightRange.Max );

		Pitch = Game.Random.Float( VoicePitch.Min, VoicePitch.Max );

		Clothe();
		NeutralFace();

		Network.Refresh();
	}

	public virtual void Clothe()
	{
		if ( !ModelRenderer.IsValid() ) return;

		foreach ( var child in ModelRenderer.GameObject.Children.ToList() ) // Remove old clothing
			child.Destroy();

		var clothing = new ClothingContainer();

		void AddRandom( ClothingContainer container, ClothesPlanner planner )
		{
			var randomPiece = planner.Random();
			if ( randomPiece.IsValid() )
				container.Add( randomPiece );
		}

		AddRandom( clothing, SkinClothing );
		AddRandom( clothing, HairClothing );
		AddRandom( clothing, FaceClothing );
		AddRandom( clothing, FacialClothing );
		AddRandom( clothing, HairbrowsClothing );
		AddRandom( clothing, ShirtClothing );
		AddRandom( clothing, PantsClothing );
		AddRandom( clothing, HoodieClothing );
		AddRandom( clothing, ShoesClothing );

		clothing.Apply( ModelRenderer );

		var hairColor = new ColorHsv( Game.Random.Float( 0f, 1f ), Game.Random.Float( 0f, 0.6f ), Game.Random.Float( 0.2f, 0.6f ) );

		foreach ( var child in ModelRenderer.GameObject.Children ) // Hair?
			if ( child.Components.TryGet<SkinnedModelRenderer>( out var renderer ) )
				if ( renderer.Tint != Color.White )
					renderer.Tint = hairColor;
	}

	private TimeUntil _nextForwardCheck;
	private bool _shouldStop = false;
	public TimeUntil CanMove { get; set; }

	protected override void OnFixedUpdate()
	{

		if ( ModelRenderer.IsValid() && AnimationHelper.IsValid() && Agent.IsValid() )
		{
			if ( !IsProxy )
				Velocity = Agent.Velocity;

			AnimationHelper.WithVelocity( Velocity );
		}

		if ( !IsProxy )
		{
			if ( Agent.IsValid() )
			{
				if ( CanMove )
				{
					if ( InLine && _nextForwardCheck )
					{
						var forwardTrace = Scene.Trace.Ray( WorldPosition + Vector3.Up * 32f, WorldPosition + Vector3.Up * 32f + WorldRotation.Forward * 70f )
							.Size( 12f )
							.IgnoreGameObjectHierarchy( GameObject )
							.WithTag( "voter" )
							.WithoutTags( "voted" )
							.Run();

						_shouldStop = forwardTrace.Hit;
						_nextForwardCheck = 0.2f;
					}

					if ( !InLine )
						_shouldStop = false;
				}
				else
					_shouldStop = true;

				Agent.MaxSpeed = _shouldStop ? 0f : WishSpeed;
				Agent.UpdateRotation = Agent.Velocity.Length >= 60f;
			}
		}

		if ( Talking )
		{
			if ( StopTalking )
				StopTalk();
			else
				RandomPheneme();
		}

		if ( LookingTo.IsValid() && !StopLooking )
			LookAt( LookingTo );
		else
			StopLook();

		if ( !IsProxy )
			if ( Ragdolled && UnragdollTimer )
				Unragdoll();
	}

	protected override void DrawGizmos()
	{
		var draw = Gizmo.Draw;

		var nameTransform = global::Transform.Zero
			.WithPosition( Vector3.Up * 84f )
			.WithRotation( Rotation.FromYaw( 90f ) * Rotation.FromRoll( 90f ) );

		draw.WorldText( FullName, nameTransform, "Poppins", 12f );
	}

	[Category( "Debug" ), Order( 6 )]
	[Rpc.Broadcast]
	[Button( "Ragdoll", "person_off" )]
	public virtual void Ragdoll( float duration = 5f, Vector3 position = default, Vector3 force = default )
	{
		if ( !ModelRenderer.IsValid() ) return;
		if ( _ragdoll.IsValid() ) return;

		_ragdoll = AddComponent<ModelPhysics>();
		_ragdoll.Renderer = ModelRenderer;
		_ragdoll.Model = ModelRenderer.Model;

		Collider.Enabled = false;
		Ragdolled = true;
		UnragdollTimer = duration;

		if ( position == default || force == default ) return;

		if ( _ragdoll.PhysicsGroup.IsValid() )
		{
			foreach ( var body in _ragdoll.PhysicsGroup.Bodies )
			{
				if ( body.Position.Distance( position ) <= 30f )
					body.ApplyImpulseAt( position, force * body.Mass );
			}
		}
	}

	[Category( "Debug" ), Order( 6 )]
	[Rpc.Broadcast]
	[Button( "Unragdoll", "person_outline" )]
	public virtual void Unragdoll()
	{
		if ( !ModelRenderer.IsValid() ) return;
		if ( !_ragdoll.IsValid() ) return;

		_ragdoll.Destroy();
		Collider.Enabled = true;
		Ragdolled = false;
		ModelRenderer.LocalPosition = Vector3.Zero;
		ModelRenderer.LocalRotation = Rotation.Identity;
	}

	/// <summary>
	/// Walk to a destination
	/// </summary>
	/// <param name="target"></param>
	public void WalkTo( Vector3 target )
	{
		if ( !Agent.IsValid() ) return;

		IsRunning = false;
		Agent.MoveTo( target );
	}

	/// <summary>
	/// Run to a destination
	/// </summary>
	/// <param name="target"></param>
	public void RunTo( Vector3 target )
	{
		if ( !Agent.IsValid() ) return;

		IsRunning = true;
		Agent.MoveTo( target );
	}

	public virtual void Talk( GameObject target )
	{
		var randomMessage = Game.Random.FromList( InteractPhrases );
		StartTalk( randomMessage, target );
		HappyFace();
	}

	[Rpc.Broadcast]
	public void StartTalk( string phrase, GameObject target = null )
	{
		if ( string.IsNullOrWhiteSpace( phrase ) ) return;

		var speechSpeed = 30f;
		var waitDuration = 2f;
		var talkDuration = phrase.Count() / speechSpeed;
		var totalDuration = talkDuration + waitDuration;

		Talking = true;
		LookingTo = target;
		StopTalking = talkDuration;
		StopLooking = totalDuration;

		if ( Interaction.IsValid() )
			Interaction.InteractionCooldown = totalDuration;

		SpeechUI.AddSpeech( FullName, phrase, speechSpeed, waitDuration, GameObject, Gender, Pitch );
	}

	public void LookAt( GameObject target )
	{
		AnimationHelper.LookAtEnabled = true;
		var lookStart = WorldPosition + Vector3.Up * 64f * WorldScale.z;
		var lookEnd = target.WorldPosition + Vector3.Up * 64f * target.WorldScale.z;
		var lookDirection = Vector3.Direction( lookStart, lookEnd );
		AnimationHelper.WithLook( lookDirection );
	}

	public void StopTalk()
	{
		ResetPheneme();
		Talking = false;
	}

	public virtual void StopLook()
	{
		if ( AnimationHelper.IsValid() )
		{
			AnimationHelper.LookAtEnabled = false;
			AnimationHelper.WithLook( Vector3.Zero );
		}
		LookingTo = null;
		NeutralFace();
	}

	internal void RandomPheneme()
	{
		if ( !ModelRenderer.IsValid() ) return;

		var randomMouthNoise = MathX.Clamp( Noise.Perlin( Time.Now * 200 + _randomSeed ) * 0.7f - 0.2f, 0f, 1f );
		ModelRenderer.Morphs.Set( "openJawL", randomMouthNoise );
		ModelRenderer.Morphs.Set( "openJawR", randomMouthNoise );
		var randomMouthPucker = MathX.Clamp( (Noise.Perlin( Time.Now * 100 + _randomSeed ) - 0.4f) * 1.4f, 0f, 1f );
		ModelRenderer.Morphs.Set( "lippuckerer", randomMouthPucker );
	}

	internal void ResetPheneme()
	{
		if ( !ModelRenderer.IsValid() ) return;

		ModelRenderer.Morphs.Set( "openJawL", 0f );
		ModelRenderer.Morphs.Set( "openJawR", 0f );
		ModelRenderer.Morphs.Set( "lippuckerer", 0f );
	}

	public void AngryFace()
	{
		if ( !ModelRenderer.IsValid() ) return;

		ModelRenderer.Set( "face_override", 5 );
	}

	public void HappyFace()
	{
		if ( !ModelRenderer.IsValid() ) return;

		ModelRenderer.Set( "face_override", 1 );
	}

	public void NeutralFace()
	{
		if ( !ModelRenderer.IsValid() ) return;

		ModelRenderer.Set( "face_override", (int)DefaultExpression );
	}
}

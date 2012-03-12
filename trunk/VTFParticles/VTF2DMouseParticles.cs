/**
 * A lot of this code was taken from the awesome tutorials at:
 *      http://www.catalinzima.com/tutorials/4-uses-of-vtf/introduction/.
 *    
 * The code has been modified somewhat to fit the purposes of the application.
 * 
 * --JP, Dec. 2011
 * */



using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace VTFParticles
{
    /// <summary>
    /// Particles!!!
    /// </summary>
    public class VTF2DMouseParticles : Microsoft.Xna.Framework.Game
    {
        static void Main(string[] args)
        {
            using (VTF2DMouseParticles game = new VTF2DMouseParticles())
            {
                game.Run();
            }
        }

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        private Texture2D randomTexture;                    //for random data
        private Texture2D particleTexture;                  //billboard texture

        
        //each RT is particleCount on each side.
        //particleCount*particleCount is actual # of parts.
        private RenderTarget2D positionRT;                  //the tex to save updated pos's
        private RenderTarget2D velocityRT;                  //the tex to save updated vel's
        private RenderTarget2D temporaryRT;                 //swap texture

        private DepthStencilBuffer simulationDepthBuffer;  //same size as RT's

        // private VertexPositionColor[] verticies;        //particle vertcies
        private VertexBuffer partVB;                       //buffer for particle verticies

        private Effect renderParticleEffect;               //shader for particle rendering
        private Effect physicsEffect;                      //shader for particle physics

        private Boolean isPhysicsReset;                    //false to reset pos and vel textures

        private const int particleCount = 1024;            //bigger = better :o)
        private const float mouseForce = 600f;
       
        private Camera camera;

        public VTF2DMouseParticles()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
            graphics.ApplyChanges();
            IsMouseVisible = true;
            Content.RootDirectory = "Content";
            camera = new Camera(this);
            Components.Add(camera);
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
//          particleTexture = Content.Load<Texture2D>("Textures\\flare");
//          particleTexture = Content.Load<Texture2D>("Textures\\Bitmap1");
            particleTexture = Content.Load<Texture2D>("Textures\\FlowerBurst");
//          particleTexture = Content.Load<Texture2D>("Textures\\StarBurst");
//          particleTexture = Content.Load<Texture2D>("Textures\\AlienBug");

            spriteBatch = new SpriteBatch(GraphicsDevice);

            temporaryRT = new RenderTarget2D(GraphicsDevice, particleCount, particleCount, 1, SurfaceFormat.Vector4,
                MultiSampleType.None, 0);
            positionRT = new RenderTarget2D(GraphicsDevice, particleCount, particleCount, 1, SurfaceFormat.Vector4,
                MultiSampleType.None, 0);
            velocityRT = new RenderTarget2D(GraphicsDevice, particleCount, particleCount, 1, SurfaceFormat.Vector4,
                MultiSampleType.None, 0);

            simulationDepthBuffer = new DepthStencilBuffer(GraphicsDevice, particleCount, particleCount,
                GraphicsDevice.DepthStencilBuffer.Format);

            isPhysicsReset = false;

            //set verticies up, which have the texture coords to find the pos and vel data.
            VertexPositionColor[] verticies = new VertexPositionColor[particleCount * particleCount];
            Random rand = new Random();

            for (int i = 0; i < particleCount; i++)
            {
                for (int j = 0; j < particleCount; j++)
                {
                    VertexPositionColor vert = new VertexPositionColor();
                    vert.Color = new Color(150, 150, (byte)(200 + rand.Next(50)));
                    vert.Position = new Vector3();

                    //set up percentages for texture coords.
                    vert.Position.X = (float)i / (float)particleCount;
                    vert.Position.Y = (float)j / (float)particleCount;
                    verticies[i * particleCount + j] = vert;
                }
            }

            //copy data to VertexBuffer
            partVB = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor),
                particleCount * particleCount, BufferUsage.Points);
            partVB.SetData(verticies);

            randomTexture = new Texture2D(GraphicsDevice, particleCount, particleCount, 1,
                TextureUsage.None, SurfaceFormat.Vector4);

            //points array for populating randomTexture with random data.
            //from -0.5f to 0.5f
            Vector4[] pointsArray = new Vector4[particleCount * particleCount];
            for (int i = 0; i < pointsArray.Length; i++)
            {
                pointsArray[i] = new Vector4();
                pointsArray[i].X = (float)rand.NextDouble() - 0.5f;
                pointsArray[i].Y = (float)rand.NextDouble() - 0.5f;
                pointsArray[i].Z = (float)rand.NextDouble() - 0.5f;
                pointsArray[i].W = (float)rand.NextDouble() - 0.5f;
            }

            randomTexture.SetData(pointsArray);

            physicsEffect = Content.Load<Effect>("Shaders\\MouseForce");
            renderParticleEffect = Content.Load<Effect>("Shaders\\Particle");
            
            physicsEffect.Parameters["screenDimensions"].SetValue(
                new Vector2(graphics.PreferredBackBufferWidth,graphics.PreferredBackBufferHeight));
            physicsEffect.Parameters["halfDimensions"].SetValue(
                new Vector3(graphics.PreferredBackBufferWidth/2f,graphics.PreferredBackBufferHeight/2f,500));
        }



        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
        }


        private void UpdatePostion(string technique, RenderTarget2D resultTarget)
        {
            RenderTarget2D oldRT = graphics.GraphicsDevice.GetRenderTarget(0) as RenderTarget2D;
            DepthStencilBuffer oldDS = graphics.GraphicsDevice.DepthStencilBuffer;

            graphics.GraphicsDevice.DepthStencilBuffer = simulationDepthBuffer;
            graphics.GraphicsDevice.SetRenderTarget(0, temporaryRT);
            graphics.GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1, 0);

            spriteBatch.Begin(SpriteBlendMode.None,
                              SpriteSortMode.Immediate,
                              SaveStateMode.None);

            physicsEffect.CurrentTechnique = physicsEffect.Techniques[technique];
            physicsEffect.Begin();

            if (isPhysicsReset)
            {
                physicsEffect.Parameters["positionMap"].SetValue(positionRT.GetTexture());
                physicsEffect.Parameters["velocityMap"].SetValue(velocityRT.GetTexture());
            }

            physicsEffect.CurrentTechnique.Passes[0].Begin();
            // the positionMap and velocityMap are passed through parameters
            // We need to pass a texture to the spriteBatch.Draw() funciton, even if we won't be using it some times.
            spriteBatch.Draw(randomTexture, new Rectangle(0, 0, particleCount, particleCount), Color.White);
            physicsEffect.CurrentTechnique.Passes[0].End();
            physicsEffect.End();

            spriteBatch.End();

            graphics.GraphicsDevice.SetRenderTarget(0, resultTarget);
            spriteBatch.Begin(SpriteBlendMode.None,
                              SpriteSortMode.Immediate,
                              SaveStateMode.None);

            //copy old data into temp render target, then operate on it with physicsEffect
            physicsEffect.CurrentTechnique = physicsEffect.Techniques["CopyTexture"];
            physicsEffect.Begin();
            physicsEffect.CurrentTechnique.Passes[0].Begin();
            spriteBatch.Draw(temporaryRT.GetTexture(), new Rectangle(0, 0, particleCount, particleCount), Color.White);
            physicsEffect.CurrentTechnique.Passes[0].End();
            physicsEffect.End();

            spriteBatch.End();

            graphics.GraphicsDevice.SetRenderTarget(0, oldRT);
            graphics.GraphicsDevice.DepthStencilBuffer = oldDS;
        }


        KeyboardState then;
        Vector2 mousePos = new Vector2(0,0);
        private float timeSinceLastReset = 0f;
        private bool applyMouseForce = false;
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            KeyboardState now = Keyboard.GetState();
            
            if (then.IsKeyDown(Keys.Space) && now.IsKeyUp(Keys.Space) && timeSinceLastReset >= 500f)
            {
                isPhysicsReset = false;
                timeSinceLastReset = 0f;

            }
            timeSinceLastReset += gameTime.ElapsedGameTime.Milliseconds;

            MouseState mouse = Mouse.GetState();
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                applyMouseForce = true;
                mousePos = new Vector2(640-mouse.X,360-mouse.Y);
            }else
            {
                applyMouseForce = false;
            }

            then = now;
            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            //GraphicsDevice.Clear(Color.Black);

            Simulate(gameTime);

            GraphicsDevice.Clear(Color.Black);

            renderParticleEffect.Parameters["WVP"].SetValue(camera.WorldViewProjection); //combine world->view->projection matrices into one
            renderParticleEffect.Parameters["textureMap"].SetValue(particleTexture);
            renderParticleEffect.Parameters["positionMap"].SetValue(positionRT.GetTexture());


            renderParticleEffect.CommitChanges();

            graphics.GraphicsDevice.RenderState.AlphaBlendEnable = true;
            graphics.GraphicsDevice.RenderState.AlphaBlendOperation = BlendFunction.Add;
            graphics.GraphicsDevice.RenderState.DepthBufferWriteEnable = false;
            graphics.GraphicsDevice.RenderState.PointSpriteEnable = true;
            graphics.GraphicsDevice.RenderState.SourceBlend = Blend.SourceAlpha;
            graphics.GraphicsDevice.RenderState.DestinationBlend = Blend.One;

            using (VertexDeclaration decl = new VertexDeclaration(GraphicsDevice, VertexPositionColor.VertexElements))
            {
                GraphicsDevice.VertexDeclaration = decl;
                renderParticleEffect.Begin();
                renderParticleEffect.CurrentTechnique.Passes[0].Begin();
                GraphicsDevice.Vertices[0].SetSource(partVB, 0, VertexPositionColor.SizeInBytes);
                GraphicsDevice.DrawPrimitives(PrimitiveType.PointList, 0, particleCount * particleCount);
                renderParticleEffect.CurrentTechnique.Passes[0].End();
                renderParticleEffect.End();
            }

            GraphicsDevice.RenderState.PointSpriteEnable = false;
            GraphicsDevice.RenderState.AlphaBlendEnable = false;
            GraphicsDevice.RenderState.DepthBufferWriteEnable = true;

            base.Draw(gameTime);
        }

        private void Simulate(GameTime gameTime)
        {
            physicsEffect.Parameters["elapsedTime"].SetValue((float)gameTime.ElapsedGameTime.TotalSeconds);
            if (applyMouseForce)
            {
                physicsEffect.Parameters["mouseForce"].SetValue(mouseForce);
                physicsEffect.Parameters["mousePos"].SetValue(mousePos);
            }
            else
            {
                physicsEffect.Parameters["mouseForce"].SetValue(0f);
            }

            if (!isPhysicsReset)
            {
                UpdatePostion("ResetPositions", positionRT);
                UpdatePostion("ResetVelocities", velocityRT);
                isPhysicsReset = true;
            }
            UpdatePostion("UpdateVelocities", velocityRT);
            UpdatePostion("UpdatePositions", positionRT);
        }
    }
}

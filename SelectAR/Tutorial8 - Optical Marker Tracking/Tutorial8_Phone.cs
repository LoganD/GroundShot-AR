/************************************************************************************ 
 * Copyright (c) 2008-2012, Columbia University
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Columbia University nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY COLUMBIA UNIVERSITY ''AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * 
 * ===================================================================================
 * Author: Ohan Oda (ohan@cs.columbia.edu)
 * 
 *************************************************************************************/

// Uncomment this line if you want to use the pattern-based marker tracking
//#define USE_PATTERN_MARKER

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using GoblinXNA.Physics;
using GoblinXNA.Physics.Matali;
using GoblinXNA.UI.UI3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Microsoft.Xna.Framework.Content;

using GoblinXNA;
using GoblinXNA.Graphics;
using GoblinXNA.SceneGraph;
using Model = GoblinXNA.Graphics.Model;
using GoblinXNA.Graphics.Geometry;
using GoblinXNA.Device.Generic;
using GoblinXNA.Device.Capture;
using GoblinXNA.Device.Vision;
using GoblinXNA.Device.Vision.Marker;
using GoblinXNA.Device.Util;
using GoblinXNA.Helpers;
using GoblinXNA.UI;
using GoblinXNA.UI.UI2D;

namespace Tutorial8___Optical_Marker_Tracking___PhoneLib
{
    public class Tutorial8_Phone : Microsoft.Xna.Framework.Game
    {
        //GraphicsDeviceManager graphics;
        private IGraphicsDeviceService gameService;
        SpriteFont sampleFont;
        Scene scene;
        MarkerNode groundMarkerNode;
        bool useStaticImage = false;
        bool useSingleMarker = false;
        bool betterFPS = true; // has trade-off of worse tracking if set to true
        Material unselectedMat, normalMat, groundMat;
        private PrimitiveModel boxModel, cylinderModel;
        private GeometryNode groundNode;
        private TransformNode gameNode, playNode, configureNode, menuNode, pauseNode;
        //private List<GeometryNode> holeNodes;
        private List<MoleHole> moleHoles;
        private List<MoleHoleForSelect> moleHolesForSelect;
        private List<MoleHoleForButton> buttonsInMenu, buttonsInPause, buttonsInPlay, buttonsInConfigure;
        private Viewport viewport;//
        private String label = "Nothing is selected";
        private Texture2D grassText;
        private Timer timer;
        private Random random;
        private Mode mode;
        private enum Mode
        {
            configure,
            play,
            menu,
            pause,
        }
        //shooter
        private Material shooterMat;
        int shooterID = 0;
        


#if USE_PATTERN_MARKER
        float markerSize = 32.4f;
#else
        float markerSize = 57f;
#endif

        public Tutorial8_Phone()
        {
            //graphics = new GraphicsDeviceManager(this);
            // no contents
            //Content.RootDirectory = "Content";
            //graphics.IsFullScreen = true;
        }

        public Texture2D VideoBackground
        {
            get { return scene.BackgroundTexture; }
            set { scene.BackgroundTexture = value; }
        }

        public void Initialize(IGraphicsDeviceService service, ContentManager content, VideoBrush videoBrush)
        {
            viewport = new Viewport(0, 0, 800, 480);
            viewport.MaxDepth = service.GraphicsDevice.Viewport.MaxDepth;
            viewport.MinDepth = service.GraphicsDevice.Viewport.MinDepth;
            service.GraphicsDevice.Viewport = viewport;
            gameService = service;
            // Initialize the GoblinXNA framework
            State.InitGoblin(service, content, "");
            LoadContent(content);

            //State.ThreadOption = (ushort)ThreadOptions.MarkerTracking;

            // Initialize the scene graph
            scene = new Scene();
            scene.BackgroundColor = Color.Black;
            scene.PhysicsEngine = new MataliPhysics();
            //Set up gravity
            scene.PhysicsEngine.Gravity = 1000;
            scene.PhysicsEngine.GravityDirection = -Vector3.UnitZ;
            // Set up the lights used in the scene
            CreateLights();
            CreateCamera();
            SetupMarkerTracking(videoBrush);
            CreateObjects();
            CreateUIPanel();
            State.ShowNotifications = true;
            Notifier.Font = sampleFont;
            State.ShowFPS = true;
            State.ShowTriangleCount = true;
            random = new Random();
            buttonsInMenu = new List<MoleHoleForButton>();
            buttonsInConfigure = new List<MoleHoleForButton>();
            buttonsInPlay = new List<MoleHoleForButton>();
            buttonsInPause = new List<MoleHoleForButton>();
            SetupMenuView();
            SetupConfigureView();
            SetupPauseView();
            ShowMenu();
            //StartGame();
            MouseInput.Instance.MousePressEvent += new HandleMousePress(MousePressHandler);
            MouseInput.Instance.MouseClickEvent += new HandleMouseClick(MouseClickHandler);
        }

        private void CreateCamera()
        {
            // Create a camera 
            Camera camera = new Camera();
            // Put the camera at the origin
            camera.Translation = new Vector3(0, 0, 0);
            // Set the vertical field of view to be 60 degrees
            camera.FieldOfViewY = MathHelper.ToRadians(60);
            // Set the near clipping plane to be 0.1f unit away from the camera
            camera.ZNearPlane = 0.1f;
            // Set the far clipping plane to be 1000 units away from the camera
            camera.ZFarPlane = 1000;

            // Now assign this camera to a camera node, and add this camera node to our scene graph
            CameraNode cameraNode = new CameraNode(camera);
            scene.RootNode.AddChild(cameraNode);

            // Assign the camera node to be our scene graph's current camera node
            scene.CameraNode = cameraNode;
        }
        
        private void CreateLights()
        {
            // Create a directional light source
            LightSource lightSource = new LightSource();
            lightSource.Direction = new Vector3(-1, -1, -1);
            lightSource.Diffuse = Color.White.ToVector4();
            lightSource.Specular = new Vector4(0.6f, 0.6f, 0.6f, 1);
            // Create a light node to hold the light source
            LightNode lightNode = new LightNode();
            lightNode.AmbientLightColor = new Vector4(0.8f, 0.8f, 0.8f, 1);
            lightNode.LightSource = lightSource;

            scene.RootNode.AddChild(lightNode);
        }

        private void SetupMarkerTracking(VideoBrush videoBrush)
        {
            IVideoCapture captureDevice = null;
            
            if (useStaticImage)
            {
                captureDevice = new NullCapture();
                captureDevice.InitVideoCapture(0, FrameRate._30Hz, Resolution._320x240,
                    ImageFormat.B8G8R8A8_32, false);
                if(useSingleMarker)
                    ((NullCapture)captureDevice).StaticImageFile = "MarkerImageHiro.jpg";
                else
                    ((NullCapture)captureDevice).StaticImageFile = "MarkerImage_320x240";

                scene.ShowCameraImage = true;
            }
            else
            {
                captureDevice = new PhoneCameraCapture(videoBrush);
                captureDevice.InitVideoCapture(0, FrameRate._30Hz, Resolution._640x480,
                    ImageFormat.B8G8R8A8_32, false);
                ((PhoneCameraCapture)captureDevice).UseLuminance = true;

                if (betterFPS)
                    captureDevice.MarkerTrackingImageResizer = new HalfResizer();
            }

            // Add this video capture device to the scene so that it can be used for
            // the marker tracker
            scene.AddVideoCaptureDevice(captureDevice);

#if USE_PATTERN_MARKER
            NyARToolkitTracker tracker = new NyARToolkitTracker();
#else
            NyARToolkitIdTracker tracker = new NyARToolkitIdTracker();
#endif

            if (captureDevice.MarkerTrackingImageResizer != null)
                tracker.InitTracker((int)(captureDevice.Width * captureDevice.MarkerTrackingImageResizer.ScalingFactor),
                    (int)(captureDevice.Height * captureDevice.MarkerTrackingImageResizer.ScalingFactor),
                    "camera_para.dat");
               
            else
                tracker.InitTracker(captureDevice.Width, captureDevice.Height, "camera_para.dat");

            // Set the marker tracker to use for our scene
            scene.MarkerTracker = tracker;
        }

        private void CreateObjects()
        {
            // Create a marker node to track a ground marker array.
#if USE_PATTERN_MARKER
            if(useSingleMarker)
                groundMarkerNode = new MarkerNode(scene.MarkerTracker, "patt.hiro", 16, 16, markerSize, 0.7f);
            else
                groundMarkerNode = new MarkerNode(scene.MarkerTracker, "NyARToolkitGroundArray.xml", 
                    NyARToolkitTracker.ComputationMethod.Average);
#else
            groundMarkerNode = new MarkerNode(scene.MarkerTracker, "NyARToolkitIDGroundArray.xml",
                NyARToolkitTracker.ComputationMethod.Average);
#endif
            scene.RootNode.AddChild(groundMarkerNode);
            //creat axis for groundMarkerNode
            TransformNode groundAxisNode = createAxisNode(1.0f);

            groundMarkerNode.AddChild(groundAxisNode);
            //groundAxisNode.Translation = new Vector3(0, 0, -20);
            //scene.RootNode.AddChild(groundAxisNode);
            // Create a geometry node with a model of a box that will be overlaid on
            // top of the ground marker array initially. (When the toolbar marker array is
            // detected, it will be overlaid on top of the toolbar marker array.)
            /*
            GeometryNode boxNode = new GeometryNode("Box");
            boxNode.Model = new Box(markerSize);

            // Create a material to apply to the box model
            Material boxMaterial = new Material();
            boxMaterial.Diffuse = new Vector4(0.5f, 0, 0, 1);
            boxMaterial.Specular = Color.White.ToVector4();
            boxMaterial.SpecularPower = 10;

            boxNode.Material = boxMaterial;

            TransformNode boxTransNode = new TransformNode();
            boxTransNode.Translation = new Vector3(0, 0, markerSize / 2);

            // Add this box model node to the ground marker node
            groundMarkerNode.AddChild(boxTransNode);
            boxTransNode.AddChild(boxNode);
             */

            //Assignment 3
            //Fadein Mat
            normalMat = new Material();
            normalMat.Diffuse = new Color(1, 1, 1, 0.99f).ToVector4();
            normalMat.Specular = new Color(1, 1, 1, 0.99f).ToVector4();
            normalMat.SpecularPower = 1;
            //Fadeout Material 
            unselectedMat = new Material();
            unselectedMat.Diffuse = new Color(1, 1, 1, 0.4f).ToVector4();
            unselectedMat.Specular = new Color(1, 1, 1, 0.4f).ToVector4();
            unselectedMat.SpecularPower = 0;
            //setup the ground
            boxModel = new Box(Vector3.One);
            
            CustomMesh groundMesh = new CustomMesh();
            VertexPositionNormalTexture[] verts = new VertexPositionNormalTexture[4];
            Vector3 vBase0 = new Vector3(-0.5f, 0.5f, 0);
            Vector3 vBase1 = new Vector3(0.5f, 0.5f, 0);
            Vector3 vBase2 = new Vector3(0.5f, -0.5f, 0);
            Vector3 vBase3 = new Vector3(-0.5f, -0.5f, 0);
            verts[0].Position = vBase0;
            verts[1].Position = vBase1;
            verts[2].Position = vBase2;
            verts[3].Position = vBase3;
            verts[0].Normal = verts[1].Normal = verts[2].Normal = verts[3].Normal = CalcNormal(vBase2, vBase1, vBase0);
            Vector2 textureTopLeft = new Vector2(0,0);
            Vector2 textureTopRight = new Vector2(1,0);
            Vector2 textureBottomLeft = new Vector2(0,1);
            Vector2 textureBottomRight = new Vector2(1,1);
            
            verts[0].TextureCoordinate = textureBottomRight;
            verts[1].TextureCoordinate = textureBottomLeft;
            verts[2].TextureCoordinate = textureTopLeft;
            verts[3].TextureCoordinate = textureTopRight;

            groundMesh.VertexBuffer = new VertexBuffer(gameService.GraphicsDevice,
                typeof(VertexPositionNormalTexture), 4, BufferUsage.None);
            groundMesh.VertexDeclaration = VertexPositionNormalTexture.VertexDeclaration;
            groundMesh.VertexBuffer.SetData(verts);
            groundMesh.NumberOfVertices = 4;
            
            short[] indices = new short[6];

            indices[0] = 0;
            indices[1] = 2;
            indices[2] = 3;

            indices[3] = 0;
            indices[4] = 1;
            indices[5] = 2;

            groundMesh.IndexBuffer = new IndexBuffer(gameService.GraphicsDevice, typeof(short), 6,
                BufferUsage.WriteOnly);
            groundMesh.IndexBuffer.SetData(indices);
            
            groundMesh.PrimitiveType = PrimitiveType.TriangleList;
            groundMesh.NumberOfPrimitives = 2;
             
            PrimitiveModel groundModel = new PrimitiveModel(groundMesh);
            


            groundNode = new GeometryNode("ground");
            groundNode.Model = groundModel;
            // add to physical engine
            groundNode.Physics.Collidable = true;
            groundNode.Physics.Shape = GoblinXNA.Physics.ShapeType.Box;
            groundNode.AddToPhysicsEngine = true;

            groundMat = new Material();
            groundMat.Diffuse = Color.White.ToVector4();
            groundMat.Specular = Color.White.ToVector4();
            groundMat.SpecularPower = 10;
            groundMat.Texture = grassText;
            groundNode.Material = groundMat;
            TransformNode groundTransNode = new TransformNode();
            groundTransNode.Scale = new Vector3(380, 250, 0);
            groundTransNode.Translation = new Vector3(0, 0, 0);
            groundMarkerNode.AddChild(groundTransNode);
            groundTransNode.AddChild(groundNode);
            playNode = new TransformNode();
            configureNode = new TransformNode();
            menuNode = new TransformNode();
            pauseNode = new TransformNode();
            gameNode = new TransformNode();
            groundMarkerNode.AddChild(gameNode);
            gameNode.AddChild(playNode);
            gameNode.AddChild(configureNode);
            gameNode.AddChild(menuNode);
            gameNode.AddChild(pauseNode);
            moleHoles = new List<MoleHole>();

            /*
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                MoleHole moleHole = new MoleHole(new Vector3(-120 + i * 60, 0, 0));
                playNode.AddChild(moleHole.holeTransNode);
                moleHoles.Add(moleHole);
            }*/


            // Create a material for shooting box models
            shooterMat = new Material();
            shooterMat.Diffuse = Color.Pink.ToVector4();
            shooterMat.Specular = Color.Yellow.ToVector4();
            shooterMat.SpecularPower = 10;
        }

        private void CreateUIPanel()
        {
        }

        private void SetupMenuView()
        {
            MoleHoleForButton moleHoleForNewGame = new MoleHoleForButton(new Vector3(-60, 10, 0), "newGameButton");
            moleHoleForNewGame.holeTransNode.Scale = new Vector3(1.5f, 1.5f, 1.2f);
            menuNode.AddChild(moleHoleForNewGame.holeTransNode);
            MoleHoleForButton moleHoleForHelp = new MoleHoleForButton(new Vector3(60, -30, 0), "helpButton");
            moleHoleForHelp.holeTransNode.Scale = new Vector3(0.9f, 0.9f, 0.9f);
            menuNode.AddChild(moleHoleForHelp.holeTransNode);   
            //add them to list
            buttonsInMenu.Add(moleHoleForNewGame);
            buttonsInMenu.Add(moleHoleForHelp);
        }

        private void ShowMenu()
        {
            switchMode(Mode.menu);
        }

        private void SetupConfigureView()
        {
            //setup moleholes for selection
            moleHolesForSelect = new List<MoleHoleForSelect>();
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    //MoleHole moleHole = new MoleHole(new Vector3(-120 + i * 60, 0, 0));
                    //configureNode.AddChild(moleHole.holeTransNode);
                    MoleHoleForSelect moleHoleForSelect = new MoleHoleForSelect(new Vector3(-120 + i * 60, -60 + 60 * j, 0));
                    configureNode.AddChild(moleHoleForSelect.holeTransNode);
                    moleHolesForSelect.Add(moleHoleForSelect);
                }
            }
            MoleHoleForButton moleHoleForPlay = new MoleHoleForButton(new Vector3(160, -100, 0), "playButton");
            moleHoleForPlay.holeTransNode.Scale = new Vector3(0.6f, 0.6f, 0.6f);
            configureNode.AddChild(moleHoleForPlay.holeTransNode);   
            buttonsInConfigure.Add(moleHoleForPlay);
        }

        private void ConfigureGame()
        {
            switchMode(Mode.configure);
        }

        private void StartGame()
        {
            switchMode(Mode.play);
            moleHoles = new List<MoleHole>();
            playNode.RemoveChildren();
            buttonsInPlay = new List<MoleHoleForButton>();
            foreach (MoleHoleForSelect hole in moleHolesForSelect)
            {
                if (hole.selected)
                {
                    MoleHole moleHole = new MoleHole(hole.position);
                    playNode.AddChild(moleHole.holeTransNode);
                    moleHoles.Add(moleHole);
                }
            }
            MoleHoleForButton moleHoleForPause = new MoleHoleForButton(new Vector3(-160, -100, 0), "pauseButton");
            moleHoleForPause.holeTransNode.Scale = new Vector3(0.6f, 0.6f, 0.6f);
            playNode.AddChild(moleHoleForPause.holeTransNode);
            timer = new Timer(TimerCallback, null, 1000, 500);
            buttonsInPlay.Add(moleHoleForPause);
        }

        private void ResumeGame()
        {
            switchMode(Mode.play);
        }

        private void SetupPauseView()
        {
            MoleHoleForButton moleHoleForResume = new MoleHoleForButton(new Vector3(-80, -10, 0), "resumeButton");
            pauseNode.AddChild(moleHoleForResume.holeTransNode);
            moleHoleForResume.holeTransNode.Scale = new Vector3(0.9f, 0.9f, 0.9f);
            MoleHoleForButton moleHoleForRestart = new MoleHoleForButton(new Vector3(20, 20, 0), "restartButton");
            pauseNode.AddChild(moleHoleForRestart.holeTransNode);
            MoleHoleForButton moleHoleForBack = new MoleHoleForButton(new Vector3(90, -40, 0), "backToMenuButton");
            moleHoleForBack.holeTransNode.Scale = new Vector3(0.8f, 0.8f, 0.8f);
            pauseNode.AddChild(moleHoleForBack.holeTransNode);
            //all to list
            buttonsInPause.Add(moleHoleForBack);
            buttonsInPause.Add(moleHoleForRestart);
            buttonsInPause.Add(moleHoleForResume);
        }

        private void PauseGame()
        {
            switchMode(Mode.pause);
        }

        private void LoadContent(ContentManager content)
        {
            sampleFont = content.Load<SpriteFont>("Sample");
            grassText = content.Load<Texture2D>("grass");
        }

        public void Dispose()
        {
            scene.Dispose();
        }

        public void Update(TimeSpan elapsedTime, bool isActive)
        {
            /*
            Quaternion rotate = new Quaternion();
            Vector3 trans = new Vector3();
            Vector3 scale = new Vector3();
            scene.PhysicsEngine.GravityDirection = (groundMarkerNode.WorldTransformation).Forward;
            */
            animateMoles();
            scene.Update(elapsedTime, false, isActive);
        }

        public void Draw(TimeSpan elapsedTime)
        {
            State.Device.Viewport = viewport;
            UI2DRenderer.WriteText(Vector2.Zero, label, new Color(0.5f, 0.5f, 0.5f),
                sampleFont, GoblinEnums.HorizontalAlignment.Left, GoblinEnums.VerticalAlignment.Bottom);
            scene.Draw(elapsedTime, false);
        }

        private TransformNode createAxisNode(float scale)
        {
            //create axis
            cylinderModel = new Cylinder(3, 3, 100, 10);
            //Text3D xAxisText = new Text3D("X");
            GeometryNode xAxis = new GeometryNode("X-axis");
            xAxis.Model = cylinderModel;
            Material xAxisMat = new Material();
            xAxisMat.Diffuse = new Color(1, 0, 0, 0.5f).ToVector4();
            xAxis.Material = xAxisMat;
            GeometryNode yAxis = new GeometryNode("Y-axis");
            yAxis.Model = cylinderModel;
            Material yAxisMat = new Material();
            yAxisMat.Diffuse = new Color(0, 1, 0, 0.5f).ToVector4();
            yAxis.Material = yAxisMat;
            GeometryNode zAxis = new GeometryNode("Z-axis");
            zAxis.Model = cylinderModel;
            Material zAxisMat = new Material();
            zAxisMat.Diffuse = new Color(0, 0, 1, 0.5f).ToVector4();
            zAxis.Material = zAxisMat;

            //axis TransNodes
            TransformNode axisTransNode = new TransformNode();
            axisTransNode.Scale = new Vector3(scale, scale, scale);
            TransformNode xAxisTransNode = new TransformNode();
            xAxisTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathHelper.ToRadians(-90));
            xAxisTransNode.Translation = new Vector3(50, 0, 0);
            TransformNode yAxisTransNode = new TransformNode();
            yAxisTransNode.Translation = new Vector3(0, 50, 0);
            TransformNode zAxisTransNode = new TransformNode();
            zAxisTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90));
            zAxisTransNode.Translation = new Vector3(0, 0, 50);
            axisTransNode.AddChild(xAxisTransNode);
            xAxisTransNode.AddChild(xAxis);
            axisTransNode.AddChild(yAxisTransNode);
            yAxisTransNode.AddChild(yAxis);
            axisTransNode.AddChild(zAxisTransNode);
            zAxisTransNode.AddChild(zAxis);

            return axisTransNode;
        }

        private void TimerCallback(object state)
        {
            if (false)
            {
                timer.Dispose();
                return;
            }
            int randomInt = random.Next(0, 2*moleHoles.Count);
            if (randomInt >= moleHoles.Count)
                return;
            if (moleHoles[randomInt].state != MoleHole.State.hidden)
                return;
            moleHoles[randomInt].showMole(800);
        }
        private void ShootBox(Vector3 near, Vector3 far)
        {
            GeometryNode shootBox = new GeometryNode("ShooterBox" + shooterID++);
            shootBox.Model = boxModel;
            shootBox.Material = shooterMat;
            shootBox.Physics.Interactable = true;
            shootBox.Physics.Collidable = true;
            shootBox.Physics.Shape = GoblinXNA.Physics.ShapeType.Box;
            shootBox.Physics.Mass = 600f;
            shootBox.AddToPhysicsEngine = true;

            // Calculate the direction to shoot the box based on the near and far point
            Vector3 linVel = far - near;
            linVel.Normalize();
            // Multiply the direction with the velocity of 20
            linVel *= 800f;

            // Assign the initial velocity to this shooting box
            shootBox.Physics.InitialLinearVelocity = linVel;

            TransformNode shooterTrans = new TransformNode();
            shooterTrans.Translation = near;
            shooterTrans.Scale = new Vector3(10, 10, 10);

            groundMarkerNode.AddChild(shooterTrans);
            shooterTrans.AddChild(shootBox);
        }
        private void MouseClickHandler(int button, Point mouseLocation)
        {
            // Shoot a box if left mouse button is clicked
            if (button == MouseInput.LeftButton)
            {
                Vector3 nearSource = new Vector3(mouseLocation.X, mouseLocation.Y, -12);
                Vector3 farSource = new Vector3(mouseLocation.X, mouseLocation.Y, -10);

                Vector3 nearPoint = gameService.GraphicsDevice.Viewport.Unproject(nearSource,
                    State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);
                Vector3 farPoint = gameService.GraphicsDevice.Viewport.Unproject(farSource,
                    State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);

                ShootBox(nearPoint, farPoint);
            }
        }
        private void MousePressHandler(int button, Point point)
        {/*
            if (button == MouseInput.LeftButton)
            {
                Vector3 nearSource = new Vector3(point.X, point.Y, -5000);
                Vector3 farSource = new Vector3(point.X, point.Y, -5);

                Vector3 nearPoint = gameService.GraphicsDevice.Viewport.Unproject(nearSource,
                    State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);
                Vector3 farPoint = gameService.GraphicsDevice.Viewport.Unproject(farSource,
                    State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);
                GeometryNode shootBox = new GeometryNode("ShooterBox" + shooterID++);
                shootBox.Model = boxModel;
                shootBox.Material = shooterMat;
                shootBox.Physics.Interactable = true;
                shootBox.Physics.Collidable = true;
                shootBox.Physics.Shape = GoblinXNA.Physics.ShapeType.Box;
                shootBox.Physics.Mass = 600f;
                shootBox.AddToPhysicsEngine = false;

                TransformNode shooterTrans = new TransformNode();
                shooterTrans.Translation = nearPoint;
                shooterTrans.Scale = new Vector3(10, 10, 10);

                groundMarkerNode.AddChild(shooterTrans);
                shooterTrans.AddChild(shootBox);
            }*/
            GeometryNode selectedObj = selectObj(point);
            if (selectedObj == null)
                return;
            switch (mode)
            {
                case Mode.play:
                    if (selectedObj.Name == "mole")
                        ((MoleHole.MoleNode) selectedObj).myMoleHole.hit();
                    if (selectedObj.Name == "pauseButton")
                        PauseGame();
                    break;
                case Mode.configure:
                    if (selectedObj.Name == "moleHoleForSelect")
                        ((MoleHoleForSelect.HoleNode) selectedObj).myMoleHoleForSelect.select();
                    if (selectedObj.Name == "playButton")
                        StartGame();
                    break;
                case Mode.menu:
                    if (selectedObj.Name == "newGameButton")
                        ConfigureGame();
                    if (selectedObj.Name == "helpButton")
                    {
                        //show help!
                    }
                    break;
                case Mode.pause:
                    if (selectedObj.Name == "resumeButton")
                        ResumeGame();
                    if (selectedObj.Name == "restartButton")
                        StartGame();
                    if (selectedObj.Name == "backToMenuButton")
                        ShowMenu();
                    break;
            }
        }

        private GeometryNode selectObj(Point mouseLocation)
        {
            // In order to perform ray  picking, first we need to define a ray by projecting
            // the 2D mouse location to two 3D points: one on the near clipping plane and one on
            // the far clipping plane.  The vector between these two points defines the finite-length
            // 3D ray that we wish to intersect with objects in the scene.

            // 0 means on the near clipping plane, and 1 means on the far clipping plane
            Vector3 nearSource = new Vector3(mouseLocation.X, mouseLocation.Y, 0);
            Vector3 farSource = new Vector3(mouseLocation.X, mouseLocation.Y, 1);


            // Now convert the near and far source to actual near and far 3D points based on our eye location
            // and view frustum
            Vector3 nearPoint = viewport.Unproject(nearSource,
                State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);
            Vector3 farPoint = viewport.Unproject(farSource,
                State.ProjectionMatrix, State.ViewMatrix, groundMarkerNode.WorldTransformation);

            //nearPoint = new Vector3(0,0,0);
            //farPoint = new Vector3(0,0,2000);


            // Have the physics engine intersect the pick ray defined by the nearPoint and farPoint with
            // the physics objects in the scene (which we have set up to approximate the model geometry).

            List<PickedObject> pickedObjects = ((MataliPhysics)scene.PhysicsEngine).PickRayCast(
                nearPoint, farPoint);

            // If one or more objects intersect with our ray vector
            if (pickedObjects.Count > 0)
            {
                // Since PickedObject can be compared (which means it implements IComparable), we can sort it in 
                // the order of closest intersected object to farthest intersected object
                pickedObjects.Sort();

                // We only care about the closest picked object for now, so we'll simply display the name 
                // of the closest picked object whose container is a geometry node
                label = ((GeometryNode) pickedObjects[0].PickedPhysicsObject.Container).Name + " is selected";
                return ((GeometryNode) pickedObjects[0].PickedPhysicsObject.Container);
                //ViewSelected((GeometryNode)pickedObjects[0].PickedPhysicsObject.Container);
                // NOTE: for a shape defined as ConvexHull (e.g.,the torus shape), even if you click the
                // hole in the torus, it will think that it is picked. This is because a ConvexHull shape
                // does not have holes, and the physics engine we use does not support shape with holes.
                // However, it is possible to refine this behavior by performing your own ray intersection algorithm
                // on this picked object. It's a good idea to perform your ray intersection after the
                // physics engine returns you a picked object, since the physics engine's algorithm is well
                // optimized. Then, you can work your way from the front of the pickedObjects list to the
                // back, performing your own ray intersection with each object in sequence,
                // until you find an object that it intersects.
                // If you want to implement your own picking algorithm, we suggest that you see the 
                // "Picking with Triangle-Accuracy Sample" at http://creators.xna.com/Education/Samples.aspx
            }
            label = "Nothing is selected";
            return null;

        }

        private Vector3 CalcNormal(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 v0_1 = v1 - v0;
            Vector3 v0_2 = v2 - v0;

            Vector3 normal = Vector3.Cross(v0_2, v0_1);
            normal.Normalize();

            return normal;
        }

        private void switchMode(Mode newMode)
        {
            mode = newMode;
            
            playNode.Enabled = false;
            configureNode.Enabled = false;
            menuNode.Enabled = false;
            pauseNode.Enabled = false;
            foreach (MoleHole mh in moleHoles)
            {
                mh.setUnpickable();
            }
            foreach (MoleHoleForSelect mh in moleHolesForSelect)
            {
                mh.setUnpickable();
            }
            foreach (MoleHoleForButton mh in buttonsInMenu)
            {
                mh.setUnpickable();
            }
            foreach (MoleHoleForButton mh in buttonsInConfigure)
            {
                mh.setUnpickable();
            }
            foreach (MoleHoleForButton mh in buttonsInPlay)
            {
                mh.setUnpickable();
            }
            foreach (MoleHoleForButton mh in buttonsInPause)
            {
                mh.setUnpickable();
            }

            //gameNode.RemoveChildren();
            switch (newMode)
            {
                case Mode.play:
                    playNode.Enabled = true;
                    foreach (MoleHole mh in moleHoles)
                    {
                        mh.setPickable();
                    }
                    foreach (MoleHoleForButton mh in buttonsInPlay)
                    {
                        mh.setPickable();
                    }
                    break;
                case Mode.configure:
                    configureNode.Enabled = true;
                    foreach (MoleHoleForSelect mh in moleHolesForSelect)
                    {
                        mh.setPickable();
                    }
                    foreach (MoleHoleForButton mh in buttonsInConfigure)
                    {
                        mh.setPickable();
                    }
                    //gameNode.AddChild(configureNode);
                    break;
                case Mode.menu:
                    menuNode.Enabled = true;
                    foreach (MoleHoleForButton mh in buttonsInMenu)
                    {
                        mh.setPickable();
                    }
                    //gameNode.AddChild(menuNode);
                    break;
                case Mode.pause:
                    pauseNode.Enabled = true;
                    foreach (MoleHoleForButton mh in buttonsInPause)
                    {
                        mh.setPickable();
                    }
                    //gameNode.AddChild(pauseNode);
                    break;
            }
        }

        private void animateMoles()
        {
            if (mode != Mode.play)
                return;
            foreach (var moleHole in moleHoles)
            {
                if (moleHole.moleTransNode.Translation.Y < 15 && moleHole.state == MoleHole.State.shown)
                {
                    moleHole.moleTransNode.Translation += new Vector3(0, 4, 0);
                    if (moleHole.moleTransNode.Translation.Y > 15)
                        moleHole.moleTransNode.Translation = new Vector3(0, 15, 0);
                }
                if (moleHole.moleTransNode.Translation.Y > -12 && moleHole.state == MoleHole.State.restore)
                {
                    moleHole.moleTransNode.Translation += new Vector3(0, -4, 0);
                    if (moleHole.moleTransNode.Translation.Y < -12)
                        moleHole.moleTransNode.Translation = new Vector3(0, -12, 0);
                }
            }
        }

        private class MoleHole
        {
            public enum State
            {
                shown,hidden,restore,hit
            };

            public State state;
            public TransformNode moleTransNode;
            public TransformNode holeTransNode;
            public MoleNode moleNode;
            private Timer moleTimer;

            public MoleHole(Vector3 position)
            {
                PrimitiveModel holeModel = new Cylinder(20, 20, 8, 20);
                GeometryNode holeNode = new GeometryNode();
                holeNode.Model = holeModel;
                Material holeMat = new Material();
                holeMat.Diffuse = new Color(115f / 225f, 94f / 225f, 64f / 225f, 1).ToVector4();
                holeNode.Material = holeMat;
                holeTransNode = new TransformNode();
                holeTransNode.Translation = position;
                holeTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90));
                PrimitiveModel moleModel = new Sphere(15, 20, 20);
                moleNode = new MoleNode("mole");
                moleNode.myMoleHole = this;
                moleNode.Model = moleModel;
                Material moleMat = new Material();
                moleMat.Diffuse = new Color(62f / 225f, 54f / 225f, 41f / 225f, 1).ToVector4();
                moleNode.Material = moleMat;
                moleNode.Physics.Shape = ShapeType.Sphere;
                moleNode.Physics.Pickable = true;
                moleNode.AddToPhysicsEngine = true;
                moleTransNode = new TransformNode();
                //moleTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(-90));
                moleTransNode.Translation = new Vector3(0, -12, 0);
                /*
                Text3D text = new Text3D("DebugFont","Perfect",UI3DRenderer.Text3DStyle.Fill);
                GeometryNode textNode = new GeometryNode();
                textNode.Model = text;
                Material textMat = new Material();
                textMat.Diffuse = new Color(115f / 225f, 94f / 225f, 64f / 225f, 1).ToVector4();
                textNode.Material = textMat;
                //holeTransNode.AddChild(textNode);
                */
                moleTransNode.AddChild(moleNode);
                holeTransNode.AddChild(moleTransNode);
                holeTransNode.AddChild(holeNode);
                state = State.hidden;
            }

            public void showMole(int duration)
            {
                state = State.shown;
                //moleTransNode.Translation = new Vector3(0, 15, 0);
                moleTimer = new Timer(MoleTimerCallback, null, duration, 0);
            }

            public void hit()
            {
                if (state == State.shown)
                    hitMole();
            }

            private void MoleTimerCallback(object obj)
            {
                //moleTimer.Dispose();
                if (state == State.shown)
                    hideMole();
            }

            public void hideMole()
            {
                state = State.restore;
                moleTimer = new Timer(RestoreTimerCallback, null, 300, 0);
                //moleTransNode.Translation = new Vector3(0, -12, 0);
            }

            public void hitMole()
            {
                state = State.hit;
                moleTransNode.Scale = new Vector3(1.2f, 0.4f, 1.2f);
                moleTransNode.Translation = new Vector3(0, 4.1f, 0);
                moleTimer = new Timer(RestoreTimerCallback, null, 300, 0);
            }

            private void RestoreTimerCallback(object obj)
            {
                //moleTimer.Dispose();
                state = State.hidden;
                moleTransNode.Scale = new Vector3(1, 1, 1);
                moleTransNode.Translation = new Vector3(0, -12, 0);
            }

            public void setPickable()
            {
                moleNode.Physics.Pickable = true;
            }

            public void setUnpickable()
            {
                moleNode.Physics.Pickable = false;
            }


            public class MoleNode:GeometryNode
            {
                public MoleHole myMoleHole;

                public MoleNode(String name)
                {
                    Name = name;
                }
            }

            
        }

        private class MoleHoleForSelect
        {
            public bool selected;
            private Material holeMat;
            public Vector3 position;
            public HoleNode holeNode;
            public TransformNode holeTransNode;

            public MoleHoleForSelect(Vector3 pos)
            {
                selected = false;
                position = pos;
                PrimitiveModel holeModel = new Cylinder(20, 20, 8, 20);
                holeNode = new HoleNode("moleHoleForSelect");
                holeNode.Model = holeModel;
                holeNode.myMoleHoleForSelect = this;
                holeMat = new Material();
                holeMat.Diffuse = new Color(115f / 225f, 94f / 225f, 64f / 225f, 0.4f).ToVector4();
                holeNode.Material = holeMat;
                holeTransNode = new TransformNode();
                holeNode.Physics.Shape = ShapeType.Cylinder;
                holeNode.Physics.Pickable = true;
                holeNode.AddToPhysicsEngine = true;
                holeTransNode.Translation = position;
                holeTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90));
                holeTransNode.AddChild(holeNode);
            }

            public void select()
            {
                selected = !selected;
                if (!selected )
                    holeMat.Diffuse = new Color(115f / 225f, 94f / 225f, 64f / 225f, 0.4f).ToVector4(); 
                else
                    holeMat.Diffuse = new Color(115f / 225f, 94f / 225f, 64f / 225f, 1).ToVector4();
            }

            public void setPickable()
            {
                holeNode.Physics.Pickable = true;
            }

            public void setUnpickable()
            {
                holeNode.Physics.Pickable = false;
            }


            public class HoleNode : GeometryNode
            {
                public MoleHoleForSelect myMoleHoleForSelect;

                public HoleNode(String name)
                {
                    Name = name;
                }
            }

        }

        private class MoleHoleForButton
        {
            private Material holeMat;
            public Vector3 position;
            public HoleNode holeNode;
            public TransformNode holeTransNode;

            public MoleHoleForButton(Vector3 pos, String buttonName)
            {
                position = pos;
                PrimitiveModel holeModel = new Cylinder(35, 35, 12, 20);
                holeNode = new HoleNode(buttonName);
                holeNode.Model = holeModel;
                holeNode.myMoleHoleForButton = this;
                holeMat = new Material();
                holeMat.Diffuse = new Color(80f / 225f, 70f / 225f, 50f / 225f, 1f).ToVector4();
                holeNode.Material = holeMat;
                holeTransNode = new TransformNode();
                holeNode.Physics.Shape = ShapeType.Cylinder;
                holeNode.Physics.Pickable = true;
                holeNode.AddToPhysicsEngine = true;
                holeTransNode.Translation = position;
                holeTransNode.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathHelper.ToRadians(90));
                holeTransNode.AddChild(holeNode);
            }

            public class HoleNode : GeometryNode
            {
                public MoleHoleForButton myMoleHoleForButton;

                public HoleNode(String name)
                {
                    Name = name;
                }
            }

            public void setPickable()
            {
                holeNode.Physics.Pickable = true;
            }

            public void setUnpickable()
            {
                holeNode.Physics.Pickable = false;
            }

        }


    }
}

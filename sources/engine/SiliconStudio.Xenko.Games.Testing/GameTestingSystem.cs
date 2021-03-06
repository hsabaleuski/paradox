﻿// Copyright (c) 2014-2015 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using SiliconStudio.Core;
using SiliconStudio.Xenko.Engine;
using SiliconStudio.Xenko.Engine.Network;
using SiliconStudio.Xenko.Games.Testing.Requests;
using SiliconStudio.Xenko.Graphics;
using SiliconStudio.Xenko.Input;
using SiliconStudio.Xenko.Input.Extensions;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace SiliconStudio.Xenko.Games.Testing
{
    /// <summary>
    /// This game system will be automatically injected by the Module initialized when included in the build processing via msbuild
    /// The purpose is to simulate events within the game process and report errors and such to the GameTestingClient
    /// </summary>
    internal class GameTestingSystem : GameSystemBase
    {
        private readonly ConcurrentQueue<Action> drawActions = new ConcurrentQueue<Action>();
        private SocketMessageLayer socketMessageLayer;

        public GameTestingSystem(IServiceRegistry registry) : base(registry)
        {
            DrawOrder = int.MaxValue;
            Enabled = true;
            Visible = true;
        }

        public override async void Initialize()
        {
            var game = (Game)Game;

            //Quit after 1 minute anyway!
            Task.Run(async () =>
            {
                await Task.Delay(60000);
                Quit(game);
            });

            var url = $"/service/{XenkoVersion.CurrentAsText}/SiliconStudio.Xenko.SamplesTestServer.exe";

            var socketContext = await RouterClient.RequestServer(url);

            socketMessageLayer = new SocketMessageLayer(socketContext, false);

            socketMessageLayer.AddPacketHandler<KeySimulationRequest>(request =>
            {
                if (request.Down)
                {
                    game.Input.SimulateKeyDown(request.Key);
                }
                else
                {
                    game.Input.SimulateKeyUp(request.Key);
                }
            });

            socketMessageLayer.AddPacketHandler<TapSimulationRequest>(request =>
            {
                switch (request.State)
                {
                    case PointerState.Down:
                        game.Input.SimulateTapDown(request.Coords);
                        break;

                    case PointerState.Up:
                        game.Input.SimulateTapUp(request.Coords, request.CoordsDelta, request.Delta);
                        break;

                    case PointerState.Move:
                        game.Input.SimulateTapMove(request.Coords, request.CoordsDelta, request.Delta);
                        break;
                }
            });

            socketMessageLayer.AddPacketHandler<ScreenshotRequest>(request =>
            {
                drawActions.Enqueue(() =>
                {
                    SaveTexture(game.GraphicsDevice.BackBuffer, request.Filename);
                });
            });

            socketMessageLayer.AddPacketHandler<TestEndedRequest>(request =>
            {
                Quit(game);
            });

            Task.Run(() => socketMessageLayer.MessageLoop());

            drawActions.Enqueue(async () =>
            {
                await socketMessageLayer.Send(new TestRegistrationRequest { GameAssembly = game.Settings.PackageName, Tester = false, Platform = (int)Platform.Type });
            });
        }

        public override void Draw(GameTime gameTime)
        {
            Action action;
            if (drawActions.TryDequeue(out action))
            {
                action();
            }
        }

        private void SaveTexture(Texture texture, string filename)
        {
            using (var image = texture.GetDataAsImage())
            {
                //Send to server and store to disk
                var imageData = new TestResultImage { CurrentVersion = "1.0", Frame = "0", Image = image, TestName = "" };
                var payload = new ScreenShotPayload { FileName = filename };
                var resultFileStream = new MemoryStream();
                var writer = new BinaryWriter(resultFileStream);
                imageData.Write(writer);

                Task.Run(() =>
                {
                    payload.Data = resultFileStream.ToArray();
                    payload.Size = payload.Data.Length;
                    socketMessageLayer.Send(payload).Wait();
                    resultFileStream.Dispose();
                });
            }
        }

        private static void Quit(Game game)
        {
            game.Exit();

#if SILICONSTUDIO_PLATFORM_ANDROID
            global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
#endif
        }
    }
}

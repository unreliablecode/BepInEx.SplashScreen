﻿using Mono.Cecil;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Collections;
using System.Threading;
using System.IO.Pipes;
using System;
using static BepInEx.GUI.Event;
using System.Collections.Generic;

namespace BepInEx.GUI
{
    public static class GuiPatcher
    {
        public static IEnumerable<string> TargetDLLs => Enumerable.Empty<string>();
        public static void Patch(AssemblyDefinition _) { }

        internal static Logging.ManualLogSource Logger = Logging.Logger.CreateLogSource("BepInEx.GUI");

        internal static StreamWriter writer;

        internal static Queue queuedMessages = Queue.Synchronized(new Queue());

        public static void Initialize()
        {
            var executable = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "GraphicalUserInterface.exe");
            Thread server = new Thread(ServerThread);
            server.IsBackground = true;
            server.Start();
            System.Diagnostics.Process.Start(executable);
        }


        public static void RaiseEvent(Category category, Event.Type type, string args)
        {
            queuedMessages.Enqueue(new Event(category, type, args));
        }

        private static void ServerThread()
        {
            NamedPipeServerStream pipeServer;
            try
            {
                pipeServer = new NamedPipeServerStream("RoR2-BepInExGUI", PipeDirection.Out, 1);
            }
            catch (Exception)
            {
                return;
            }
            pipeServer.WaitForConnection();
            Logger.LogInfo("Connected to the GUI");
            writer = new StreamWriter(pipeServer);
            try
            {
                writer.WriteLine(StartEvent(Category.Patcher, Preloader.Patching.AssemblyPatcher.PatcherPlugins.Count));
                
            } catch (Exception e)
            {
                Logger.LogWarning(e.Message);
            }

            while (pipeServer.IsConnected)
            {
                if(queuedMessages.Count > 0)
                {
                    var message = queuedMessages.Dequeue();

                    writer.WriteLine(message);
                    if (message is Event e && e._category == Category.Game && e._type == Event.Type.StartOne)
                    {
                        Logger.LogMessage("Game has started, ending connection.");
                        break;
                    }
                }
                Thread.Sleep(100);
            }

            writer.Dispose();
            Logger.Dispose();
            queuedMessages = null;
        }
    }
}
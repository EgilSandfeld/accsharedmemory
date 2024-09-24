﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Timers;
using Serilog;

namespace AssettoCorsaSharedMemory
{
    public delegate void PhysicsUpdatedHandler(object sender, PhysicsEventArgs e);
    public delegate void GraphicsUpdatedHandler(object sender, GraphicsEventArgs e);
    public delegate void StaticInfoUpdatedHandler(object sender, StaticInfoEventArgs e);
    public delegate void MemoryStatusChangedHandler(object sender, MemoryStatusEventArgs e);
    public delegate void GameStatusChangedHandler(object sender, GameStatusEventArgs e);
    public delegate void PitStatusChangedHandler(object sender, PitStatusEventArgs e);
    public delegate void SessionTypeChangedHandler(object sender, SessionTypeEventArgs e);

    public class AssettoCorsaNotStartedException : Exception
    {
        public AssettoCorsaNotStartedException()
            : base("Shared Memory not connected, is Assetto Corsa running and have you run assettoCorsa.Start()?")
        {
        }
    }

    public enum AC_MEMORY_STATUS { DISCONNECTED, CONNECTING, CONNECTED }
    
    public class ACCSharedMemory
    {
        private Timer sharedMemoryRetryTimer;
        private AC_MEMORY_STATUS memoryStatus = AC_MEMORY_STATUS.DISCONNECTED;
        public bool IsConnected => (memoryStatus == AC_MEMORY_STATUS.CONNECTED);
        public bool IsRunning => (memoryStatus is AC_MEMORY_STATUS.CONNECTING || memoryStatus is AC_MEMORY_STATUS.CONNECTED);

        private AC_STATUS gameStatus = AC_STATUS.AC_OFF;
        private int pitStatus = 1;
        private AC_SESSION_TYPE sessionType = AC_SESSION_TYPE.AC_UNKNOWN;
        
        public static readonly Dictionary<CarModel, int> BrakeBiasOffset = new Dictionary<CarModel, int>()
        {
            {CarModel.amr_v12_vantage_gt3, -7 },
            {CarModel.audi_r8_lms, -14 },
            {CarModel.bentley_continental_gt3_2016, -7 },
            {CarModel.bentley_continental_gt3_2018, -7 },
            {CarModel.bmw_m6_gt3, -15 },
            {CarModel.jaguar_g3, -7 },
            {CarModel.ferrari_488_gt3, -17 },
            {CarModel.honda_nsx_gt3, -14 },
            {CarModel.lamborghini_gallardo_rex, -14 },
            {CarModel.lamborghini_huracan_gt3, -14 },
            {CarModel.lamborghini_huracan_st, -14 },
            {CarModel.lexus_rc_f_gt3, -14 },
            {CarModel.mclaren_650s_gt3, -17 },
            {CarModel.mercedes_amg_gt3, -14 },
            {CarModel.nissan_gt_r_gt3_2017, -15 },
            {CarModel.nissan_gt_r_gt3_2018, -15 },
            {CarModel.porsche_991_gt3_r, -21 },
            {CarModel.porsche_991ii_gt3_cup, -5 },
            {CarModel.amr_v8_vantage_gt3, -7 },
            {CarModel.audi_r8_lms_evo, -14 },
            {CarModel.honda_nsx_gt3_evo, -14 },
            {CarModel.lamborghini_huracan_gt3_evo, -14 },
            {CarModel.mclaren_720s_gt3, -17 },
            {CarModel.porsche_991ii_gt3_r, -21 },
            {CarModel.alpine_a110_gt4, -15 },
            {CarModel.amr_v8_vantage_gt4, -20 },
            {CarModel.audi_r8_gt4, -15 },
            {CarModel.bmw_m4_gt4, -22 },
            {CarModel.chevrolet_camaro_gt4r, -18 },
            {CarModel.ginetta_g55_gt4, -18 },
            {CarModel.ktm_xbow_gt4, -20 },
            {CarModel.maserati_mc_gt4, -15 },
            {CarModel.mclaren_570s_gt4, -9 },
            {CarModel.mercedes_amg_gt4, -20 },
            {CarModel.porsche_718_cayman_gt4_mr, -20 },
            {CarModel.ferrari_488_gt3_evo, -17 },
            {CarModel.mercedes_amg_gt3_evo, -14 },
            {CarModel.bmw_m4_gt3, -14 },
            {CarModel.audi_r8_lms_evo_ii, -14 },
            {CarModel.bmw_m2_cs_racing, -17 },
            {CarModel.ferrari_488_challenge_evo, -13 },
            {CarModel.lamborghini_huracan_st_evo2, -14 },
            {CarModel.porsche_992_gt3_cup, -5 },
            {CarModel.ferrari_296_gt3, -5 },
            {CarModel.porsche_992_gt3_r, -21 },
            {CarModel.lamborghini_huracan_gt3_evo2, -14 },
            {CarModel.mclaren_720s_gt3_evo, -17 }
        };

        public static readonly Dictionary<CarModel, int> MaxRPM = new Dictionary<CarModel, int>()
        {
            {CarModel.amr_v12_vantage_gt3, 7750 },
            {CarModel.audi_r8_lms, 8650 },
            {CarModel.bentley_continental_gt3_2016, 7500 },
            {CarModel.bentley_continental_gt3_2018, 7400 },
            {CarModel.bmw_m6_gt3, 7100 },
            {CarModel.jaguar_g3, 8750 },
            {CarModel.ferrari_488_gt3, 7300 },
            {CarModel.honda_nsx_gt3, 7500 },
            {CarModel.lamborghini_gallardo_rex, 8650 },
            {CarModel.lamborghini_huracan_gt3, 8650 },
            {CarModel.lamborghini_huracan_st, 8650 },
            {CarModel.lexus_rc_f_gt3, 7750 },
            {CarModel.mclaren_650s_gt3, 7500 },
            {CarModel.mercedes_amg_gt3, 7900 },
            {CarModel.nissan_gt_r_gt3_2017, 7500 },
            {CarModel.nissan_gt_r_gt3_2018, 7500 },
            {CarModel.porsche_991_gt3_r, 9250 },
            {CarModel.porsche_991ii_gt3_cup, 8500 },
            {CarModel.amr_v8_vantage_gt3, 7250 },
            {CarModel.audi_r8_lms_evo, 8650 },
            {CarModel.honda_nsx_gt3_evo, 7650 },
            {CarModel.lamborghini_huracan_gt3_evo, 8650 },
            {CarModel.mclaren_720s_gt3, 7700 },
            {CarModel.porsche_991ii_gt3_r, 9250 },
            {CarModel.alpine_a110_gt4, 6450 },
            {CarModel.amr_v8_vantage_gt4, 7000 },
            {CarModel.audi_r8_gt4, 8650 },
            {CarModel.bmw_m4_gt4, 7600 },
            {CarModel.chevrolet_camaro_gt4r, 7500 },
            {CarModel.ginetta_g55_gt4, 7200 },
            {CarModel.ktm_xbow_gt4, 6500 },
            {CarModel.maserati_mc_gt4, 7000 },
            {CarModel.mclaren_570s_gt4, 7600 },
            {CarModel.mercedes_amg_gt4, 7000 },
            {CarModel.porsche_718_cayman_gt4_mr, 7800 },
            {CarModel.ferrari_488_gt3_evo, 7600 },
            {CarModel.mercedes_amg_gt3_evo, 7600 },
            {CarModel.bmw_m4_gt3, 7000 },
            {CarModel.audi_r8_lms_evo_ii, 8650 },
            {CarModel.bmw_m2_cs_racing, 7520 },
            {CarModel.ferrari_488_challenge_evo, 8000 },
            {CarModel.lamborghini_huracan_st_evo2, 8650 },
            {CarModel.porsche_992_gt3_cup, 8750 },
            {CarModel.ferrari_296_gt3, 8000 },
            {CarModel.porsche_992_gt3_r, 9250 },
            {CarModel.lamborghini_huracan_gt3_evo2, 8650 },
            {CarModel.mclaren_720s_gt3_evo, 7700 }
        };

        public enum CarModel
        {
            Unknown = -1,
            amr_v12_vantage_gt3 = 12,
            audi_r8_lms = 3,
            bentley_continental_gt3_2016 = 11,
            bentley_continental_gt3_2018 = 8,
            bmw_m6_gt3 = 7,
            jaguar_g3 = 14,
            ferrari_488_gt3 = 2,
            honda_nsx_gt3 = 17,
            lamborghini_gallardo_rex = 13,
            lamborghini_huracan_gt3 = 4,
            lamborghini_huracan_st = 18,
            lexus_rc_f_gt3 = 15,
            mclaren_650s_gt3 = 5,
            mercedes_amg_gt3 = 1,
            nissan_gt_r_gt3_2017 = 10,
            nissan_gt_r_gt3_2018 = 6,
            porsche_991_gt3_r = 0,
            porsche_991ii_gt3_cup = 9,
            amr_v8_vantage_gt3 = 20,
            audi_r8_lms_evo = 19,
            honda_nsx_gt3_evo = 21,
            lamborghini_huracan_gt3_evo = 16,
            mclaren_720s_gt3 = 22,
            porsche_991ii_gt3_r = 23,
            alpine_a110_gt4 = 50,
            amr_v8_vantage_gt4 = 51,
            audi_r8_gt4 = 52,
            bmw_m4_gt4 = 53,
            chevrolet_camaro_gt4r = 55,
            ginetta_g55_gt4 = 56,
            ktm_xbow_gt4 = 57,
            maserati_mc_gt4 = 58,
            mclaren_570s_gt4 = 59,
            mercedes_amg_gt4 = 60,
            porsche_718_cayman_gt4_mr = 61,
            ferrari_488_gt3_evo = 24,
            mercedes_amg_gt3_evo = 25,
            bmw_m4_gt3 = 30,
            audi_r8_lms_evo_ii = 31,
            bmw_m2_cs_racing = 27,
            ferrari_488_challenge_evo = 26,
            lamborghini_huracan_st_evo2 = 29,
            porsche_992_gt3_cup = 28,
            ferrari_296_gt3 = 100, //What is the model ID?
            porsche_992_gt3_r = 101, //What is the model ID?
            lamborghini_huracan_gt3_evo2 = 102, //What is the model ID?
            mclaren_720s_gt3_evo = 103 //What is the model ID?
        };
    
        public static readonly Dictionary<CarModel, int> MaxSteeringAngleDegrees = new Dictionary<CarModel, int>
        {
            {CarModel.amr_v12_vantage_gt3, 320},
            {CarModel.audi_r8_lms, 360},
            {CarModel.bentley_continental_gt3_2016, 320},
            {CarModel.bentley_continental_gt3_2018, 320},
            {CarModel.bmw_m6_gt3, 283},
            {CarModel.jaguar_g3, 360},
            {CarModel.ferrari_488_gt3, 240},
            {CarModel.honda_nsx_gt3, 310},
            {CarModel.lamborghini_gallardo_rex, 360},
            {CarModel.lamborghini_huracan_gt3, 310},
            {CarModel.lamborghini_huracan_st, 310},
            {CarModel.lexus_rc_f_gt3, 320},
            {CarModel.mclaren_650s_gt3, 240},
            {CarModel.mercedes_amg_gt3, 320},
            {CarModel.nissan_gt_r_gt3_2017, 320},
            {CarModel.nissan_gt_r_gt3_2018, 320},
            {CarModel.porsche_991_gt3_r, 400},
            {CarModel.porsche_991ii_gt3_cup, 400},
            {CarModel.amr_v8_vantage_gt3, 320},
            {CarModel.audi_r8_lms_evo, 360},
            {CarModel.honda_nsx_gt3_evo, 310},
            {CarModel.lamborghini_huracan_gt3_evo, 310},
            {CarModel.mclaren_720s_gt3, 240},
            {CarModel.porsche_991ii_gt3_r, 400},
            {CarModel.alpine_a110_gt4, 360},
            {CarModel.amr_v8_vantage_gt4, 320},
            {CarModel.audi_r8_gt4, 360},
            {CarModel.bmw_m4_gt4, 246},
            {CarModel.chevrolet_camaro_gt4r, 360},
            {CarModel.ginetta_g55_gt4, 360},
            {CarModel.ktm_xbow_gt4, 290},
            {CarModel.maserati_mc_gt4, 450},
            {CarModel.mclaren_570s_gt4, 240},
            {CarModel.mercedes_amg_gt4, 246},
            {CarModel.porsche_718_cayman_gt4_mr, 400},
            {CarModel.ferrari_488_gt3_evo, 240},
            {CarModel.mercedes_amg_gt3_evo, 320},
            {CarModel.bmw_m4_gt3, 270},
            {CarModel.audi_r8_lms_evo_ii, 360},
            {CarModel.bmw_m2_cs_racing, 180},
            {CarModel.ferrari_488_challenge_evo, 240},
            {CarModel.lamborghini_huracan_st_evo2, 310},
            {CarModel.porsche_992_gt3_cup, 270}
        };

        public event MemoryStatusChangedHandler MemoryStatusChanged;
        public virtual void OnMemoryStatusChanged(MemoryStatusEventArgs e)
        {
            if (MemoryStatusChanged != null)
            {
                MemoryStatusChanged(this, e);
            }
        }
        
        /// <summary>
        /// Event raised when the SDK detects the sim for the first time.
        /// </summary>
        public event EventHandler Connected;
        private void OnConnected(EventArgs e)
        {
            var handler = Connected;
            handler?.Invoke(this, e);
        }
        
        /// <summary>
        /// Event raised when the SDK no longer detects the sim (sim closed).
        /// </summary>
        public event EventHandler Disconnected;
        private void OnDisconnected(EventArgs e)
        {
            var handler = Disconnected;
            handler?.Invoke(this, e);
        }
        
        /// <summary>
        /// Event raised when the SDK no longer detects the sdk header content (when sim is closed, using UI and the user registers and starts joining another server)
        /// </summary>
        public event EventHandler Connecting;
        private void OnConnecting(EventArgs e)
        {
            var handler = Connecting;
            handler?.Invoke(this, e);
        }

        public event GameStatusChangedHandler GameStatusChanged;
        public virtual void OnGameStatusChanged(GameStatusEventArgs e)
        {
            if (GameStatusChanged != null)
            {
                GameStatusChanged(this, e);
            }
        }


        public event PitStatusChangedHandler PitStatusChanged;
        public virtual void OnPitStatusChanged(PitStatusEventArgs e)
        {
            if (PitStatusChanged != null)
            {
                PitStatusChanged(this, e);
            }
        }
        public event SessionTypeChangedHandler SessionTypeChanged;
        public virtual void OnSessionTypeChangedHandler(PitStatusEventArgs e)
        {
            if (PitStatusChanged != null)
            {
                PitStatusChanged(this, e);
            }
        }

        public static readonly Dictionary<AC_STATUS, string> StatusNameLookup = new Dictionary<AC_STATUS, string>
        {
            { AC_STATUS.AC_OFF, "Off" },
            { AC_STATUS.AC_LIVE, "Live" },
            { AC_STATUS.AC_PAUSE, "Pause" },
            { AC_STATUS.AC_REPLAY, "Replay" },
        };

        public ACCSharedMemory(int telemetryUpdateIntervalMs)
        {
            sharedMemoryRetryTimer = new Timer(2000);
            sharedMemoryRetryTimer.AutoReset = true;
            sharedMemoryRetryTimer.Elapsed += sharedMemoryRetryTimer_Elapsed;

            physicsTimer = new Timer();
            physicsTimer.AutoReset = true;
            physicsTimer.Elapsed += physicsTimer_Elapsed;
            PhysicsInterval = telemetryUpdateIntervalMs;

            graphicsTimer = new Timer();
            graphicsTimer.AutoReset = true;
            graphicsTimer.Elapsed += graphicsTimer_Elapsed;
            GraphicsInterval = telemetryUpdateIntervalMs;

            staticInfoTimer = new Timer();
            staticInfoTimer.AutoReset = true;
            staticInfoTimer.Elapsed += staticInfoTimer_Elapsed;
            StaticInfoInterval = 1000;

            Stop();
        }

        /// <summary>
        /// Connect to the shared memory and start the update timers
        /// </summary>
        public void Start()
        {
            sharedMemoryRetryTimer.Start();
            Log.Debug("ACC SharedMemory Start");
        }

        void sharedMemoryRetryTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ConnectToSharedMemory();
        }

        private bool ConnectToSharedMemory()
        {
            try
            {
                SetMemoryStatus(AC_MEMORY_STATUS.CONNECTING);
                // Connect to shared memory
                physicsMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_physics");
                graphicsMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_graphics");
                staticInfoMMF = MemoryMappedFile.OpenExisting("Local\\acpmf_static");
                
                // Start the timers
                staticInfoTimer.Start();
                ProcessStaticInfo();

                graphicsTimer.Start();
                ProcessGraphics();

                physicsTimer.Start();
                ProcessPhysics();

                // Stop retry timer
                sharedMemoryRetryTimer.Stop();
                SetMemoryStatus(AC_MEMORY_STATUS.CONNECTED);
                return true;
            }
            catch (FileNotFoundException)
            {
                staticInfoTimer.Stop();
                graphicsTimer.Stop();
                physicsTimer.Stop();
                return false;
            }
        }

        private void SetMemoryStatus(AC_MEMORY_STATUS status)
        {
            lock (_memoryStatusLock)
            {
                if (memoryStatus == status)
                    return;
        
                memoryStatus = status;
            }
            var memoryStatusEventArgs = new MemoryStatusEventArgs(status);
            OnMemoryStatusChanged(memoryStatusEventArgs);
            
            if (status == AC_MEMORY_STATUS.CONNECTED)
                OnConnected(memoryStatusEventArgs);
            
            if (status == AC_MEMORY_STATUS.CONNECTING)
                OnConnecting(memoryStatusEventArgs);
            
            if (status == AC_MEMORY_STATUS.DISCONNECTED)
                OnDisconnected(memoryStatusEventArgs);
        }

        /// <summary>
        /// Stop the timers and dispose of the shared memory handles
        /// </summary>
        public void Stop()
        {
            SetMemoryStatus(AC_MEMORY_STATUS.DISCONNECTED);
            sharedMemoryRetryTimer.Stop();

            // Stop the timers
            physicsTimer.Stop();
            graphicsTimer.Stop();
            staticInfoTimer.Stop();
            
            staticInfoMMF?.Dispose();
            graphicsMMF?.Dispose();
            physicsMMF?.Dispose();
        }

        /// <summary>
        /// Interval for physics updates in milliseconds
        /// </summary>
        public double PhysicsInterval
        {
            get
            {
                return physicsTimer.Interval;
            }
            set
            {
                physicsTimer.Interval = value;
            }
        }

        /// <summary>
        /// Interval for graphics updates in milliseconds
        /// </summary>
        public double GraphicsInterval
        {
            get
            {
                return graphicsTimer.Interval;
            }
            set
            {
                graphicsTimer.Interval = value;
            }
        }

        /// <summary>
        /// Interval for static info updates in milliseconds
        /// </summary>
        public double StaticInfoInterval
        {
            get
            {
                return staticInfoTimer.Interval;
            }
            set
            {
                staticInfoTimer.Interval = value;
            }
        }

        MemoryMappedFile physicsMMF;
        MemoryMappedFile graphicsMMF;
        MemoryMappedFile staticInfoMMF;

        Timer physicsTimer;
        Timer graphicsTimer;
        Timer staticInfoTimer;
        private readonly object _memoryStatusLock = new ();

        /// <summary>
        /// Represents the method that will handle the physics update events
        /// </summary>
        public event PhysicsUpdatedHandler PhysicsUpdated;

        /// <summary>
        /// Represents the method that will handle the graphics update events
        /// </summary>
        public event GraphicsUpdatedHandler GraphicsUpdated;

        /// <summary>
        /// Represents the method that will handle the static info update events
        /// </summary>
        public event StaticInfoUpdatedHandler StaticInfoUpdated;

        public virtual void OnPhysicsUpdated(PhysicsEventArgs e)
        {
            PhysicsUpdated?.Invoke(this, e);
        }

        public virtual void OnGraphicsUpdated(GraphicsEventArgs e)
        {
            if (GraphicsUpdated != null)
            {
                GraphicsUpdated(this, e);
                if (gameStatus != e.ACCSharedMemoryGraphics.Status)
                {
                    gameStatus = e.ACCSharedMemoryGraphics.Status;
                    GameStatusChanged?.Invoke(this, new GameStatusEventArgs(gameStatus));
                }
                if (pitStatus != e.ACCSharedMemoryGraphics.IsInPit)
                {
                    pitStatus = e.ACCSharedMemoryGraphics.IsInPit;
                    PitStatusChanged?.Invoke(this, new PitStatusEventArgs(pitStatus));
                }
                if (sessionType != e.ACCSharedMemoryGraphics.Session)
                {
                    sessionType = e.ACCSharedMemoryGraphics.Session;
                    SessionTypeChanged?.Invoke(this, new SessionTypeEventArgs(sessionType));
                }
            }
        }

        public virtual void OnStaticInfoUpdated(StaticInfoEventArgs e)
        {
            StaticInfoUpdated?.Invoke(this, e);
        }

        private void physicsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessPhysics();
        }

        private void graphicsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessGraphics();
        }

        private void staticInfoTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessStaticInfo();
        }

        private void ProcessPhysics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                ACCSharedMemoryPhysics accSharedMemoryPhysics = ReadPhysics();
                OnPhysicsUpdated(new PhysicsEventArgs(accSharedMemoryPhysics));
            }
            catch (AssettoCorsaNotStartedException)
            { }
        }

        private void ProcessGraphics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                ACCSharedMemoryGraphics accSharedMemoryGraphics = ReadGraphics();
                OnGraphicsUpdated(new GraphicsEventArgs(accSharedMemoryGraphics));
            }
            catch (AssettoCorsaNotStartedException)
            { }
        }

        private void ProcessStaticInfo()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED)
                return;

            try
            {
                ACCSharedMemoryStatic accSharedMemoryStatic = ReadStaticInfo();
                OnStaticInfoUpdated(new StaticInfoEventArgs(accSharedMemoryStatic));
            }
            catch (AssettoCorsaNotStartedException)
            { }
        }

        /// <summary>
        /// Read the current physics data from shared memory
        /// </summary>
        /// <returns>A Physics object representing the current status, or null if not available</returns>
        private ACCSharedMemoryPhysics ReadPhysics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || physicsMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = physicsMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryPhysics));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryPhysics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryPhysics));
                    handle.Free();
                    return data;
                }
            }
        }

        private ACCSharedMemoryGraphics ReadGraphics()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || graphicsMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = graphicsMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryGraphics));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryGraphics)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryGraphics));
                    handle.Free();
                    return data;
                }
            }
        }

        private ACCSharedMemoryStatic ReadStaticInfo()
        {
            if (memoryStatus == AC_MEMORY_STATUS.DISCONNECTED || staticInfoMMF == null)
                throw new AssettoCorsaNotStartedException();

            using (var stream = staticInfoMMF.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    var size = Marshal.SizeOf(typeof(ACCSharedMemoryStatic));
                    var bytes = reader.ReadBytes(size);
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var data = (ACCSharedMemoryStatic)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ACCSharedMemoryStatic));
                    handle.Free();
                    return data;
                }
            }
        }
        
        public string ToPrettyString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"IsRunning;{IsRunning}");
            sb.AppendLine($"IsConnected;{IsConnected}");
            sb.AppendLine($"memoryStatus;{memoryStatus}");
            sb.AppendLine($"sessionType;{sessionType}");
            sb.AppendLine($"pitStatus;{pitStatus}");
            sb.AppendLine($"gameStatus;{gameStatus}");
            
            return sb.ToString();
        }
    }
}
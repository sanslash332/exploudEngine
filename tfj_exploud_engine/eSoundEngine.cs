﻿using System;
using System.Collections.Generic;
using System.Text;
using FMOD;
using logSystem;
using NLog;

namespace tfj.exploudEngine
{
    public class eSoundEngine
    {
        
        private static eSoundEngine _defaultEngine;
        public static eSoundEngine defaultEngine
        {
            get
            {
                return (_defaultEngine);
            }
            set
            {
                _defaultEngine = value;
            }
        }

        public FMOD.System fmod { get; private set; }
        public uint oculusSourcePluginHandle { get; private set; }
        public uint oculusAmbisonicPluginHangle { get; private set; }
        public uint oculusGlobalSettingsPluginHandle { get; private set; }
        public DSP oculusGlobalSettingsDSP { get; private set; }
        public eInstanceGroup default3dGroup { get; private set; }
        public eInstanceGroup default2dGroup { get; private set; }
        public eInstanceGroup defaultMusicGroup { get; private set;  }
        public eListener listener { get; private set;  }
        public eRoom currentRoom { get; private set;  }
        private Dictionary<string, ePlayable> soundKache;
        public bool reflections3d
        {
            get
            {
                eUtils.fmodCheck(this.oculusGlobalSettingsDSP.getParameterBool(0, out bool activeReflections));
                return (activeReflections);
            }
            set
            {
                eUtils.fmodCheck(this.oculusGlobalSettingsDSP.setParameterBool(0, value));
            }
        }
        public bool reberberations3d
        {
            get
            {
                eUtils.fmodCheck(this.oculusGlobalSettingsDSP.getParameterBool(1, out bool activeReberb));
                return (activeReberb);
            }
            set
            {
                eUtils.fmodCheck(this.oculusGlobalSettingsDSP.setParameterBool(1, value));
            }
        }

        public eSoundEngine()
        {
            LogWriter.getLog().Info("starting exploud engine");
            defaultEngine = this;
            eUtils.fmodCheck(Factory.System_Create(out FMOD.System fmod));
            this.fmod = fmod;
            
            eUtils.fmodCheck(fmod.init(1024, INITFLAGS.NORMAL, (IntPtr)OUTPUTTYPE.AUTODETECT));
            eUtils.fmodCheck(fmod.getDSPBufferSize(out uint bufferLength, out int numOfSamples), "geting dsp buffer size ");
            LogWriter.getLog().Debug($"loading oculus spatializer with {bufferLength} buffer lenght, and {numOfSamples} num of samples ");
            eUtils.oculusCheck(eOculusOperations.oculusInit(44100, bufferLength));
            this.soundKache = new Dictionary<string, ePlayable>();

            loadPlugins();
            setDefaultSettings();

        }

        ~eSoundEngine()
        {
            clearSoundCache();
            eUtils.fmodCheck(this.fmod.release(), "clearing internal fmod");

        }

        public void loadPlugins()
        {
            eUtils.fmodCheck(fmod.loadPlugin("plugins/OculusSpatializerFMOD.dll", out uint sourceHandle));
            this.oculusSourcePluginHandle = sourceHandle;
            eUtils.fmodCheck(fmod.getNestedPlugin(oculusSourcePluginHandle, 1, out uint ambisonicHandle));
            this.oculusAmbisonicPluginHangle = ambisonicHandle;
            eUtils.fmodCheck(fmod.getNestedPlugin(oculusSourcePluginHandle, 2, out uint GSHandle));
            this.oculusGlobalSettingsPluginHandle = GSHandle;
            LogWriter.getLog().Debug("plugins loaded");
        }

        public void setDefaultSettings()
        {
            this.listener = new eListener(this);
            string group3dName = "3dGroup";
            string group2dName = "2dGroup";
            string groupMusicName = "musicGroup";
            eUtils.fmodCheck(fmod.createChannelGroup(group3dName, out ChannelGroup group3d));
            this.default3dGroup = new eInstanceGroup(group3d, group3dName, this);
            eUtils.fmodCheck(fmod.createChannelGroup(group2dName, out ChannelGroup group2d));
            this.default2dGroup = new eInstanceGroup(group2d, group2dName, this);
            eUtils.fmodCheck(fmod.createChannelGroup(groupMusicName, out ChannelGroup groupMusic));
            this.defaultMusicGroup = new eInstanceGroup(groupMusic, groupMusicName, this);
            eUtils.fmodCheck(fmod.createDSPByPlugin(this.oculusGlobalSettingsPluginHandle, out DSP globalSettingsDSP));
            this.oculusGlobalSettingsDSP = globalSettingsDSP;
            eUtils.fmodCheck(this.default3dGroup.handle.addDSP(CHANNELCONTROL_DSP_INDEX.TAIL,this.oculusGlobalSettingsDSP));
            this.reberberations3d = true;
            this.reflections3d = false;
            this.currentRoom = new eRoom(this);
            clearSoundCache();

        }

        private string calulateID(string path)
        {
            return (path.Replace("\\", ".").Replace("/", "."));
        }

        public eSound loadSound(string path)
        {

            string id = calulateID(path);
            LogWriter.getLog().Info($"loading {id} sound");
            if(soundKache.ContainsKey(id))
            {
                LogWriter.getLog().Info($"{id} sound was loaded previously. returning kached data");
                return ((eSound) soundKache[id]);
            }
            eSound sound = new eSound(id, path, this);
            soundKache.Add(id, sound);
            LogWriter.getLog().Info($"{id} sound loaded");
            return (sound);
        }

        public eMusic loadMusic(string path)
        {

            string id = calulateID(path);
            LogWriter.getLog().Info($"loading {id} music");
            if (soundKache.ContainsKey(id))
            {
                LogWriter.getLog().Info($"{id} music was loaded previously. returning kached data");
                return ((eMusic) soundKache[id]);
            }
            eMusic music = new eMusic(id, path, this);
            soundKache.Add(id, music);
            LogWriter.getLog().Info($"{id} music loaded");
            return (music);
        }

        public void update()
        {
            this.listener.update();
            foreach(KeyValuePair<string,ePlayable> k in soundKache)
            {
                k.Value.update();

            }
            this.currentRoom.update();
            
            eUtils.fmodCheck(fmod.update());
            

        }

        public void clearSoundCache()
        {
            foreach (KeyValuePair<string, ePlayable> k in soundKache)
            {
                k.Value.release();

            }

            this.soundKache = new Dictionary<string, ePlayable>();
        }
    }
}

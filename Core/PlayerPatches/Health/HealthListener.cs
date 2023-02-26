﻿using System.Collections.Generic;
using System.Linq;

namespace SIT.Tarkov.Core.PlayerPatches.Health
{
    public class HealthListener
    {
        private static object _lock = new object();
        private static HealthListener _instance = null;
        private bool _inRaid;
        private readonly Request _request;
        public object MyHealthController { get; private set; }

        public PlayerHealth CurrentHealth { get; } = new PlayerHealth();

        public static HealthListener Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HealthListener();
                        }
                    }
                }

                return _instance;
            }
        }

        // ctor
        private HealthListener()
        {
            _request = new Request();
        }

        /// <summary>
        /// Initialize HealthListener.
        /// This method is executed on loading profile in menu (on load game, on raid finish, on error...),
        /// and on raid start
        /// </summary>
        /// <param name="healthController">player health controller</param>
        /// <param name="inRaid">true - when executed from raid</param>
        public void Init(object healthController, bool inRaid)
        {
            if (healthController != null && healthController == MyHealthController)
                return;

            // init dependencies
            MyHealthController = healthController;
            _inRaid = inRaid;

            //_simpleTimer.isSyncHealthEnabled = !inRaid;

            CurrentHealth.IsAlive = true;

            // init current health
            //SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.Common);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.Head);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.Chest);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.Stomach);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.LeftArm);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.RightArm);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.LeftLeg);
            SetCurrentHealth(MyHealthController, CurrentHealth.Health, EBodyPart.RightLeg);

            SetCurrent("Energy");
            SetCurrent("Hydration");
            //SetCurrent("Temperature");

        }

        private void SetCurrent(string v)
        {
            //PatchConstants.Logger.LogInfo("HealthListener:SetCurrent:" + v);

            if (PatchConstants.GetAllPropertiesForObject(MyHealthController).Any(x => x.Name == v))
            {
                var valuestruct = PatchConstants.GetAllPropertiesForObject(MyHealthController).FirstOrDefault(x => x.Name == v).GetValue(MyHealthController);
                if (valuestruct == null)
                    return;

                var currentEnergy = PatchConstants.GetAllFieldsForObject(valuestruct).FirstOrDefault(x => x.Name == "Current").GetValue(valuestruct);
                //PatchConstants.Logger.LogInfo(currentEnergy);
                CurrentHealth.GetType().GetProperty(v).SetValue(CurrentHealth, float.Parse(currentEnergy.ToString()));
            }
            else if (PatchConstants.GetAllFieldsForObject(MyHealthController).Any(x => x.Name == v))
            {
                var valuestruct = PatchConstants.GetAllFieldsForObject(MyHealthController).FirstOrDefault(x => x.Name == v).GetValue(MyHealthController);
                if (valuestruct == null)
                    return;

                var currentEnergy = PatchConstants.GetAllFieldsForObject(valuestruct).FirstOrDefault(x => x.Name == "Current").GetValue(valuestruct);
                //PatchConstants.Logger.LogInfo(currentEnergy);

                CurrentHealth.GetType().GetProperty(v).SetValue(CurrentHealth, float.Parse(currentEnergy.ToString()));
            }

        }

        private void SetCurrentHealth(object healthController, IReadOnlyDictionary<EBodyPart, BodyPartHealth> dictionary, EBodyPart bodyPart)
        {
            if (healthController == null)
            {
                PatchConstants.Logger.LogInfo("HealthListener:GetBodyPartHealth:HealthController is NULL");
                return;
            }

            //PatchConstants.Logger.LogInfo("HealthListener:GetBodyPartHealth");


            var getbodyparthealthmethod = healthController.GetType().GetMethod("GetBodyPartHealth"
                , System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.FlattenHierarchy
                );
            if (getbodyparthealthmethod == null)
            {
                PatchConstants.Logger.LogInfo("HealthListener:GetBodyPartHealth not found!");
                return;
            }

            //PatchConstants.Logger.LogInfo("GetBodyPartHealth found!");

            var bodyPartHealth = getbodyparthealthmethod.Invoke(healthController, new object[] { bodyPart, false });
            var current = PatchConstants.GetAllFieldsForObject(bodyPartHealth).FirstOrDefault(x => x.Name == "Current").GetValue(bodyPartHealth).ToString();
            var maximum = PatchConstants.GetAllFieldsForObject(bodyPartHealth).FirstOrDefault(x => x.Name == "Maximum").GetValue(bodyPartHealth).ToString();

            dictionary[bodyPart].Initialize(float.Parse(current), float.Parse(maximum));

        }
    }
}
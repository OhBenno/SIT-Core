﻿using EFT.UI.Matchmaker;
using Newtonsoft.Json;
using SIT.Tarkov.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
//using ScreenController = EFT.UI.Matchmaker.MatchMakerAcceptScreen.GClass2426;
//using Grouping = GClass2434;
using EFT.UI;
using UnityEngine.UIElements;
using EFT;
using HarmonyLib;
using UnityEngine.Events;
using System.Text.RegularExpressions;

namespace SIT.Coop.Core.Matchmaker
{
    public class MatchmakerAcceptScreenAwakePatch : ModulePatch
    {
		[Serializable]
		private class ServerStatus
		{
			[JsonProperty("ip")]
			public string ip { get; set; }

			[JsonProperty("status")]
			public string status { get; set; }
		}

		static BindingFlags privateFlags = BindingFlags.NonPublic | BindingFlags.Instance;

		public static Type GetThisType()
		{
            return Tarkov.Core.PatchConstants.EftTypes
                 .Single(x => x == typeof(EFT.UI.Matchmaker.MatchMakerAcceptScreen));
            //.Single(x => x.FullName == "EFT.UI.Matchmaker.MatchMakerAcceptScreen");
        }

        protected override MethodBase GetTargetMethod()
        {

            var methodName = "Awake";

            return GetThisType().GetMethods(privateFlags).First(x=>x.Name == methodName);

        }

		[PatchPrefix]
        private static bool PatchPrefix(
            ref EFT.UI.Matchmaker.MatchMakerAcceptScreen __instance,
            ref DefaultUIButton ____backButton,
            ref DefaultUIButton ____acceptButton,
            //ref Button ____updateListButton,
            //ref DefaultUIButton ____findOtherPlayersButton,
            ref Profile ___profile_0,
            ref CanvasGroup ____canvasGroup,
            ref ERaidMode ___eraidMode_0,
            ref RaidSettings ___raidSettings_0,
            object ___MatchmakerPlayersController,
            object ___ScreenController
            )
        {
            Logger.LogInfo("MatchmakerAcceptScreenAwakePatch.PatchPrefix");

            // ------------------------------------------
            // Keep an instance for other patches to work
            MatchmakerAcceptPatches.MatchMakerAcceptScreenInstance = __instance;
            // ------------------------------------------


            //var newButton = GameObject.Instantiate<DefaultUIButton>(____acceptButton);
            //GameObject.DontDestroyOnLoad(newButton);
            //newButton.RectTransform().position = ____acceptButton.RectTransform().position;
            //newButton.RectTransform().sizeDelta = ____acceptButton.RectTransform().sizeDelta;
            //newButton.SetHeaderText("Start Match Other");
            //newButton.gameObject.SetActive(true);

            



            // ----------------------------------------------------
            // Reset number of players for next Raid
            //MatchmakerAcceptPatches.HostExpectedNumberOfPlayers = 1;

            //screenController = MatchmakerAcceptPatches.MatchmakerScreenController;
            ////___eraidMode_0 = ERaidMode.Online;
            //___eraidMode_0 = ERaidMode.Local;
            //var profile = ___profile_0;
            //____acceptButton.OnClick.RemoveAllListeners();
            //____acceptButton.OnClick.AddListener(() => 
            //{
            //    //if (___MatchmakerPlayersController.GroupPlayers.Count == 0)
            //    //{
            //    //    Logger.LogInfo("___MatchmakerPlayersController.GroupPlayers is Empty??");
            //    //}
            //    //___MatchmakerPlayersController.GroupPlayers.Add(___MatchmakerPlayersController.CurrentPlayer);

            //    //GoToRaid(); 
            //    PatchConstants.GetMethodForType(___ScreenController.GetType(), "ShowNextScreen")
            //    .Invoke(___ScreenController, new object[] { string.Empty, EMatchingType.Single });


            //});
            //____backButton.OnClick.AddListener(() => { BackOut(); });

            //_canvasGroup = ____canvasGroup;
            //_canvasGroup.interactable = true;

            return true; // run the original

        }

        //[PatchPostfix]
        //private static void PatchPostfix(
        //    ref EFT.UI.Matchmaker.MatchMakerAcceptScreen __instance,
        //    ref ERaidMode ___eraidMode_0
        //    )
        //{
        //    //Logger.LogInfo("MatchmakerAcceptScreenAwakePatch.PatchPostfix");
        //}

        public static void DoCreateAndCheck()
        {
            if(MatchmakerAcceptPatches.Profile == null)
            {
                Logger.LogError("MatchmakerAcceptScreenAwakePatch::DoCreateAndCheck::MatchmakerAcceptPatches.Profile == null");
                return;
            }
            MatchmakerAcceptPatches.CreateMatch(MatchmakerAcceptPatches.Profile.AccountId);
            MatchmakerAcceptPatches.CheckForMatch();
        }

        public static void GoToRaid()
        {
            MatchmakerAcceptPatches.CheckForMatch();

            if (MatchmakerAcceptPatches.IsSinglePlayer)
            {
                Tarkov.Core.PatchConstants.DisplayMessageNotification("Starting Singleplayer Game...");
            }
            // SendInvitePatch sets up the Host
            else if (MatchmakerAcceptPatches.IsServer)
            {
                Tarkov.Core.PatchConstants.DisplayMessageNotification("Starting Coop Game as Host");
                MatchmakerAcceptPatches.SetGroupId(PatchConstants.GetPHPSESSID());
            }
            // MatchmakerAcceptPatches.CheckForMatch sets up the Client
            else if (MatchmakerAcceptPatches.IsClient)
            {
                Tarkov.Core.PatchConstants.DisplayMessageNotification("Starting Coop Game as Client");
            }

        }

        public static void BackOut()
        {
            //Logger.LogInfo("BackOut!");
            //if (screenController != null) 
            //{
            //    Logger.LogInfo("screenController.GetType():" + screenController.GetType().FullName);
            //    //if (MatchmakerAcceptPatches.Grouping != null)
            //    //{
            //    //    if (MatchmakerAcceptPatches.IsGroupOwner())
            //    //    {
            //    //        // Invoke Disband Group
            //    //        MatchmakerAcceptPatches.Grouping
            //    //            .GetType()
            //    //            .GetMethod("DisbandGroup", BindingFlags.Public | BindingFlags.Instance).Invoke(MatchmakerAcceptPatches.Grouping, new object[] { null });
            //    //    }
            //    //    else
            //    //    {
            //    //        MatchmakerAcceptPatches.Grouping
            //    //            .GetType()
            //    //            .GetMethod("LeaveGroup", BindingFlags.Public | BindingFlags.Instance).Invoke(MatchmakerAcceptPatches.Grouping, new object[] { null });
            //    //    }

            //    //    MatchmakerAcceptPatches.Grouping
            //    //            .GetType()
            //    //            .GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance).Invoke(MatchmakerAcceptPatches.Grouping, new object[] { null });

            //    //    MatchmakerAcceptPatches.Grouping
            //    //            .GetType()
            //    //            .GetMethod("ExitFromMatchMaker", BindingFlags.Public | BindingFlags.Instance).Invoke(MatchmakerAcceptPatches.Grouping, new object[] { });

            //    //    MatchmakerAcceptPatches.Grouping = null;
            //    //}
            //    //_canvasGroup.interactable = false;
            //    //screenController.GetType().GetMethod("CloseScreen", BindingFlags.Public | BindingFlags.Instance).Invoke(screenController, new object[] { });
            //    //screenController = null;
            //    //_canvasGroup = null;
            //}
            
        }


    }

	
}

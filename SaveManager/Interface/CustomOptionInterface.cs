using Menu.Remix.MixedUI;
using SaveManager.Helpers;
using System.IO;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace SaveManager.Interface
{
    public class CustomOptionInterface : OptionInterface
    {
        public bool HasInitialized;

        /// <summary>
        /// The initial y-value by which all other controls are positioned from 
        /// </summary>
        private const float y_offset = 560f;

        /// <summary>
        /// The position by which controls are aligned along the x-axis on the left side
        /// </summary>
        private const float x_left_align = 20f;

        /// <summary>
        /// A timer that prevents the backup create option from being selected too often 
        /// </summary>
        private int backupCreateCooldown = 0;

        /// <summary>
        /// A timer that prevents the backup restore option from being selected too often
        /// </summary>
        private int backupRestoreCooldown = 0;

        private OpLabel statusLabel;

        private string _messageBuffer;
        private string messageBuffer
        {
            get => _messageBuffer;
            set
            {
                messageWaitPeriod = 0;
                _messageBuffer = value;
            }
        }
        private int messageWaitPeriod;

        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[]
            {
                new OpTab(this, Translate("Options"))
            };

            OpTab tab = Tabs[0];

            initializeOptions(tab);

            OpLabel statusLabelHeader = new OpLabel(x_left_align, y_offset - 250f, Translate("STATUS:"))
            {
                color = Color.yellow
            };

            statusLabel = new OpLabel(x_left_align + statusLabelHeader.size.x, y_offset - 250f, string.Empty);

            tab.AddItems(statusLabelHeader, statusLabel);
            HasInitialized = true;
        }

        private void initializeOptions(OpTab tab)
        {
            //Create elements
            OpLabel tabHeader = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate("Save Management"), FLabelAlignment.Center, true, null);

            OpCheckBox enableVersionSavesToggle = new OpCheckBox(Config.cfgEnablePerVersionSaves, new Vector2(x_left_align + 20f, y_offset - 80f))
            {
                description = Config.GetDescription(Config.cfgEnablePerVersionSaves)
            };
            OpLabel enableVersionSavesLabel = new OpLabel(x_left_align + 60f, y_offset - 80f, Translate(Config.GetOptionLabel(Config.cfgEnablePerVersionSaves)));

            OpCheckBox inheritVersionSavesToggle = new OpCheckBox(Config.cfgInheritVersionSaves, new Vector2(x_left_align + 20f, y_offset - 120f))
            {
                greyedOut = !Config.PerVersionSaving,
                description = Config.GetDescription(Config.cfgInheritVersionSaves)
            };
            OpLabel inheritVersionSavesLabel = new OpLabel(x_left_align + 60f, y_offset - 120f, Translate(Config.GetOptionLabel(Config.cfgInheritVersionSaves)));

            OpSimpleButton backupRestoreButton = new OpSimpleButton(new Vector2(x_left_align + 20f, y_offset - 170f), new Vector2(200f, 30f), Translate("RESTORE RECENT BACKUP"))
            {
                description = Translate("Swaps current save game data with most recent save backup from file")
            };

            OpSimpleButton backupCreateButton = new OpSimpleButton(new Vector2(backupRestoreButton.PosX + 210f, y_offset - 170f), new Vector2(200f, 30f), Translate("CREATE BACKUP"))
            {
                description = Translate("Creates a copy of save game data")
            };

            enableVersionSavesToggle.OnValueChanged += updateEnableStates;
            backupRestoreButton.OnClick += BackupRestoreButton_OnClick;
            backupCreateButton.OnClick += BackupCreateButton_OnClick;

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                tabHeader,
                enableVersionSavesToggle,
                enableVersionSavesLabel,
                inheritVersionSavesToggle,
                inheritVersionSavesLabel,
                backupRestoreButton,
                backupCreateButton,
            });
        }

        private void updateEnableStates(UIconfig config, string value, string oldValue)
        {
            updateEnableStates(bool.Parse(value));
        }

        private void updateEnableStates(bool applyState)
        {
            if (!HasInitialized) return;

            var inheritToggle = Config.cfgInheritVersionSaves.BoundUIconfig;

            if (inheritToggle.greyedOut == applyState)
            {
                statusLabel.text = "The inherit function only functions when Per Version Saving is enabled";
                inheritToggle.greyedOut = !applyState; //This option only works when PerVersionSaving is enabled
            }
        }

        private void BackupRestoreButton_OnClick(UIfocusable trigger)
        {
            if (backupRestoreCooldown > 0)
            {
                DisplayMessage("Operation could not be applied. Cooldown ACTIVE");
                return;
            }

            backupRestoreCooldown = RWCustom.Custom.rainWorld.processManager.currentMainLoop.framesPerSecond * 2;

            DisplayMessage("Restoring latest backup");
            Plugin.Logger.LogInfo("Restoring latest backup...");

            string processStatus;
            string mostRecentBackup = BackupUtils.GetRecentBackupPath();

            if (mostRecentBackup != null)
            {
                Plugin.Logger.LogInfo("Backup found: " + PathUtils.GetRelativePath(mostRecentBackup, 3, true));

                //User-created backups interrupt the restore cycle - Creating a backup here ensures that original data wont be overwritten
                if (BackupUtils.BackupsCreatedThisSession && BackupUtils.RestoreHandledWithoutBackup)
                {
                    Plugin.Logger.LogInfo("Creating safety backup");
                    BackupUtils.RestoreHandledWithoutBackup = false; //If this process fails, not much to do but accept it

                    BackupUtils.ConvertToBackupFormat(Plugin.BackupOverwritePath); //Backup overwrite directory is only set once per session
                    Directory.CreateDirectory(Plugin.BackupOverwritePath);
                }

                BackupUtils.RestoreFromBackup(mostRecentBackup);

                //Each time a restore is handled without a user-created backup, save data will alternate between
                //the original save data, and some other set of save files. This code tracks save data that may not be
                //properly backed up
                /*
                 * First restore - Versioning Enabled
                 * Version data swaps with original data in a cycle
                 * First restore - Versioning Disabled
                 * Original data swaps with backup data in a cycle
                 */
                if (!BackupUtils.BackupsCreatedThisSession)
                    BackupUtils.RestoreHandledWithoutBackup = !BackupUtils.RestoreHandledWithoutBackup;

                RainWorld rainWorld = RWCustom.Custom.rainWorld;

                int saveSlot = rainWorld.options.saveSlot;

                rainWorld.progression.Destroy(saveSlot);
                rainWorld.progression = new PlayerProgression(rainWorld, true, false);
                processStatus = "Save states have been reloaded";
            }
            else
            {
                processStatus = "Nothing to restore";
            }

            Plugin.Logger.LogInfo(processStatus);
            DisplayMessageOnNextUpdate(processStatus, 1);
        }

        private void BackupCreateButton_OnClick(UIfocusable trigger)
        {
            if (backupCreateCooldown > 0)
            {
                DisplayMessage("Operation could not be applied. Cooldown ACTIVE");
                return;
            }

            backupCreateCooldown = RWCustom.Custom.rainWorld.processManager.currentMainLoop.framesPerSecond * 2;

            DisplayMessage("Creating new backup...");
            Plugin.Logger.LogInfo("Creating backups");

            BackupUtils.BackupsCreatedThisSession = true;
            RWCustom.Custom.rainWorld.progression.CreateCopyOfSaves();

            string processStatus = "Process complete - Backup restore state has changed";

            Plugin.Logger.LogInfo(processStatus);
            DisplayMessageOnNextUpdate(processStatus, 1);
        }

        public override void Update()
        {
            if (backupCreateCooldown > 0)
                backupCreateCooldown--;

            if (backupRestoreCooldown > 0)
                backupRestoreCooldown--;

            base.Update();

            if (messageWaitPeriod > 0)
                messageWaitPeriod--;
            else if (messageBuffer != null)
            {
                DisplayMessage(messageBuffer);
                messageBuffer = null;
            }
        }

        /// <summary>
        /// Displays a message to the user
        /// </summary>
        public void DisplayMessage(string message)
        {
            statusLabel.text = message;
            messageWaitPeriod = 0;
        }

        public void DisplayMessageOnNextUpdate(string message, int waitPeriodInSeconds)
        {
            messageBuffer = message;
            messageWaitPeriod = RWCustom.Custom.rainWorld.processManager.currentMainLoop.framesPerSecond * waitPeriodInSeconds;
        }
    }
}

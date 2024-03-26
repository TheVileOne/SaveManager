using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public override void Initialize()
        {
            base.Initialize();

            Tabs = new OpTab[]
            {
                new OpTab(this, Translate("Options"))
            };

            OpTab tab = Tabs[0];

            initializeOptions(tab);
            HasInitialized = true;
        }

        private void initializeOptions(OpTab tab)
        {
            //Create elements
            OpLabel tabHeader = new OpLabel(new Vector2(150f, y_offset - 40f), new Vector2(300f, 30f), Translate("Save Management"), FLabelAlignment.Center, true, null);

            OpRadioButtonGroup radioOptions = new OpRadioButtonGroup(Config.cfgBackupFrequency);

            tab.AddItems(radioOptions);

            radioOptions.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(new Vector2(x_left_align + 120f, y_offset - 160f)),
                new OpRadioButton(new Vector2(x_left_align + 120f, y_offset - 200f)),
                new OpRadioButton(new Vector2(x_left_align + 120f, y_offset - 240f))
            });

            OpLabel radioOptionHeaderLabel = new OpLabel(new Vector2(x_left_align + 20f, y_offset - 120f), new Vector2(60f, 30f), "BACKUP FREQUENCY")
            {
                color = UnityEngine.Color.white
            };

            OpLabel radioOptionLabelOne = new OpLabel(new Vector2(x_left_align + 20f, y_offset - 160f), new Vector2(60f, 30f), "When version changes");
            OpLabel radioOptionLabelTwo = new OpLabel(new Vector2(x_left_align + 20f, y_offset - 200f), new Vector2(60f, 30f), "Always backup");
            OpLabel radioOptionLabelThree = new OpLabel(new Vector2(x_left_align + 20f, y_offset - 240f), new Vector2(60f, 30f), "Never backup");

            OpSimpleButton backupCreateButton = new OpSimpleButton(new Vector2(x_left_align, y_offset - 280f), new Vector2(120f, 30f), Translate("Create Save Backup"))
            {
                description = Translate("Creates a copy of game-related save data")
            };

            backupCreateButton.OnClick += BackupCreateButton_OnClick;

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                tabHeader,
                radioOptionHeaderLabel,
                radioOptionLabelOne,
                radioOptionLabelTwo,
                radioOptionLabelThree,
                backupCreateButton,
            });
        }

        private void BackupCreateButton_OnClick(UIfocusable trigger)
        {
            Plugin.Logger.LogInfo("Creating backups");
            RWCustom.Custom.rainWorld.progression.CreateCopyOfSaves();
        }

        /// <summary>
        /// Creates the elements used by the Remix menu interface to produce a standard OpCheckBox
        /// </summary>
        private OpCheckBox createCheckBox(Configurable<bool> configurable, Vector2 position)
        {
            return new OpCheckBox(configurable, position)
            {
                description = Translate(Config.GetDescription(configurable))
            };
        }

        private OpLabel createOptionLabel(UIconfig owner)
        {
            return createOptionLabel(owner, new Vector2(60f, owner.ScreenPos.y));
        }

        private OpLabel createOptionLabel(UIconfig owner, Vector2 pos)
        {
            return new OpLabel(pos.x, pos.y, Translate(Config.GetOptionLabel(owner.cfgEntry)), false)
            {
                bumpBehav = owner.bumpBehav,
                description = owner.description
            };
        }
    }
}

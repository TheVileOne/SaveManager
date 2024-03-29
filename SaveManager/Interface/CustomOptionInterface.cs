using Menu.Remix.MixedUI;
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

            OpCheckBox enableVersionSavesToggle = new OpCheckBox(Config.cfgEnablePerVersionSaves, new Vector2(x_left_align + 20f, y_offset - 80f))
            {
                description = Config.GetDescription(Config.cfgEnablePerVersionSaves)
            };
            OpLabel enableVersionSavesLabel = new OpLabel(x_left_align + 60f, y_offset - 80f, Translate(Config.GetOptionLabel(Config.cfgEnablePerVersionSaves)));
            
            /*
            OpRadioButtonGroup radioOptions = new OpRadioButtonGroup(Config.cfgBackupFrequency);

            tab.AddItems(radioOptions);

            radioOptions.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(new Vector2(x_left_align + 160f, y_offset - 160f)),
                new OpRadioButton(new Vector2(x_left_align + 160f, y_offset - 200f)),
                new OpRadioButton(new Vector2(x_left_align + 160f, y_offset - 240f))
            });

            OpLabel radioOptionHeaderLabel = new OpLabel(new Vector2(x_left_align + 70f, y_offset - 120f), new Vector2(60f, 30f), Translate("BACKUP FREQUENCY"))
            {
                color = UnityEngine.Color.white
            };

            OpLabel radioOptionLabelOne = new OpLabel(new Vector2(x_left_align + 60f, y_offset - 160f), new Vector2(60f, 30f), Translate("When version changes"));
            OpLabel radioOptionLabelTwo = new OpLabel(new Vector2(x_left_align + 60f, y_offset - 200f), new Vector2(60f, 30f), Translate("Always backup"));
            OpLabel radioOptionLabelThree = new OpLabel(new Vector2(x_left_align + 60f, y_offset - 240f), new Vector2(60f, 30f), Translate("Never backup"));
            */

            OpSimpleButton backupCreateButton = new OpSimpleButton(new Vector2(x_left_align + 20f, y_offset - 300f), new Vector2(160f, 30f), Translate("CREATE BACKUP"))
            {
                description = Translate("Creates a copy of save game data")
            };

            backupCreateButton.OnClick += BackupCreateButton_OnClick;

            //Add elements to container
            tab.AddItems(new UIelement[]
            {
                tabHeader,
                enableVersionSavesToggle,
                enableVersionSavesLabel,
                /*radioOptionHeaderLabel,
                radioOptionLabelOne,
                radioOptionLabelTwo,
                radioOptionLabelThree,*/
                backupCreateButton,
            });
        }

        private void BackupCreateButton_OnClick(UIfocusable trigger)
        {
            Plugin.Logger.LogInfo("Creating backups");
            RWCustom.Custom.rainWorld.progression.CreateCopyOfSaves();
        }
    }
}

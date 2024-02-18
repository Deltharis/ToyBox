#nullable enable annotations
﻿// borrowed shamelessly and enhanced from Bag of Tricks https://www.nexusmods.com/pathfinderkingmaker/mods/26, which is under the MIT Licenseusing Kingmaker;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Cheats;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.ElementsSystem;
using ModKit;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Items;
using Kingmaker.Controllers.Combat;
using Utilities = Kingmaker.Cheats.Utilities;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints;
using ModKit.Utility;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Enums;
using Kingmaker.UI.Common;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Designers.Mechanics.Facts;
using static Kingmaker.EntitySystem.Stats.ModifiableValue;

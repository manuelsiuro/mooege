/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Mooege.Common.Helpers;
using Mooege.Common.Helpers.Hash;
using Mooege.Common.Storage;
using Mooege.Core.MooNet.Accounts;
using Mooege.Core.MooNet.Helpers;
using Mooege.Core.MooNet.Objects;
using Mooege.Net.MooNet;
using Mooege.Core.GS.Players;


namespace Mooege.Core.MooNet.Toons
{
    #region Definitions and Enums
    //Order is important as actor voices and saved data is based on enum index
    public enum ToonClass
    {
        DemonHunter, // 0xC88B9649
        Barbarian, // 0x4FB91EE2
        Wizard, // 0x1D4681B1
        WitchDoctor, // 0x343D22A
        Monk, // 0x3DAC15
    }

    [Flags]
    public enum ToonFlags : uint
    {
        Male = 0x00,
        Female = 0x02,
        // TODO: These two need to be figured out still.. /plash
        Unknown1 = 0x20,
        Unknown2 = 0x40,
        Unknown3 = 0x80000,
        Unknown4 = 0x2000000,
        AllUnknowns = Unknown1 | Unknown2 | Unknown3 | Unknown4
    }
    #endregion
    
    public class Toon : PersistentRPCObject
    {
        //Fields that notify clients on change
        #region PresenceFields
        public IntPresenceField HeroClassField
            = new IntPresenceField(FieldKeyHelper.Program.D3, FieldKeyHelper.OriginatingClass.Hero, 1, 0);

        /// <summary>
        /// Transforms from normal value to the notification send value whenever a notification is sent
        /// </summary>
        public int HeroClassFieldTransform(int value)
        {
            return StringHashHelper.HashNormal(Enum.GetName(typeof(ToonClass), value).ToLower());
        }

        /// <summary>
        /// Toon's class.
        /// </summary>
        public ToonClass Class
        {
            get
            {
                return (ToonClass)@HeroClassField.Value;
            }
        }

        /// <summary>
        /// Happens only when a hero is created so a hashing of all values can be afforded
        /// </summary>
        /// <param name="classHash"></param>
        /// <returns></returns>
        private static int GetClassFromHash(int classHash)
        {
            foreach (ToonClass heroClass in Enum.GetValues(typeof(ToonClass)))
            {
                if (StringHashHelper.HashNormal(heroClass.ToString().ToLower()) == classHash) return (int)(ToonClass)heroClass;
            }
            return 0;
        }

        public IntPresenceField HeroLevelField
            = new IntPresenceField(FieldKeyHelper.Program.D3, FieldKeyHelper.OriginatingClass.Hero, 2, 0);

        public ByteStringPresenceField<D3.Hero.VisualEquipment> HeroVisualEquipmentField
            = new ByteStringPresenceField<D3.Hero.VisualEquipment>(FieldKeyHelper.Program.D3, FieldKeyHelper.OriginatingClass.Hero, 3, 0);

        public IntPresenceField HeroFlagsField
            = new IntPresenceField(FieldKeyHelper.Program.D3, FieldKeyHelper.OriginatingClass.Hero, 4, 0);

        public StringPresenceField HeroNameField
            = new StringPresenceField(FieldKeyHelper.Program.D3, FieldKeyHelper.OriginatingClass.Hero, 5, 0);

        #endregion

        /// <summary>
        /// D3 EntityID encoded id.
        /// </summary>
        public D3.OnlineService.EntityId D3EntityID { get; private set; }

        /// <summary>
        /// Toon's hash-code.
        /// </summary>
        public int HashCode { get; set; }


        //OBSOLETE: NEVER USED
        //TODO: Remove this in next commit
        ///// <summary>
        ///// Toon handle struct.
        ///// </summary>
        //public ToonHandleHelper ToonHandle { get; private set; }

        /// <summary>
        /// Toon's owner account.
        /// </summary>
        public GameAccount GameAccount { get; set; }

        /// <summary>
        /// Total time played for toon.
        /// </summary>
        public uint TimePlayed { get; set; }

        /// <summary>
        /// Last login time for toon.
        /// </summary>
        public uint LoginTime { get; set; }

        /// <summary>
        /// Gold amount for toon.
        /// </summary>
        public int GoldAmount { get; set; }

        /// <summary>
        /// Settings for toon.
        /// </summary>
        private D3.Client.ToonSettings _settings = D3.Client.ToonSettings.CreateBuilder().Build();
        public D3.Client.ToonSettings Settings
        {
            get
            {
                return this._settings;
            }
            set
            {
                this._settings = value;
            }
        }

        /// <summary>
        /// Toon digest.
        /// </summary>
        public D3.Hero.Digest Digest
        {
            get
            {
                return D3.Hero.Digest.CreateBuilder().SetVersion(893)
                                .SetHeroId(this.D3EntityID)
                                .SetHeroName(this.HeroNameField.Value)
                                .SetGbidClass(this.HeroClassFieldTransform((int)this.HeroClassField.Value))
                                .SetPlayerFlags((uint)this.HeroFlagsField.Value)
                                .SetLevel((int)this.HeroLevelField.Value)
                                .SetVisualEquipment(this.HeroVisualEquipmentField.Value)
                                .SetLastPlayedAct(0)
                                .SetHighestUnlockedAct(0)
                                .SetLastPlayedDifficulty(0)
                                .SetHighestUnlockedDifficulty(0)
                                .SetLastPlayedQuest(-1)
                                .SetLastPlayedQuestStep(-1)
                                .SetTimePlayed(this.TimePlayed)
                                .Build();
            }
        }

        /// <summary>
        /// Hero Profile.
        /// </summary>
        public D3.Profile.HeroProfile Profile
        {
            get
            {
                return D3.Profile.HeroProfile.CreateBuilder()
                    .SetHardcore(false)
                    .SetHeroId(this.D3EntityID)
                    .SetHighestDifficulty(0)
                    .SetHighestLevel((uint)(this.HeroLevelField.Value))
                    .SetMonstersKilled(923)
                    .Build();
            }
        }


        
        //TODO: Use same order in ToonClass so there is no need for two enums
        public int VoiceClassID // Used for Conversations
        {
            get
            {
                switch (this.Class)
                {
                    case ToonClass.DemonHunter:
                        return 0;
                    case ToonClass.Barbarian:
                        return 1;
                    case ToonClass.Wizard:
                        return 2;
                    case ToonClass.WitchDoctor:
                        return 3;
                    case ToonClass.Monk:
                        return 4;
                }
                return 0;
            }
        }

        public int Gender
        {
            get
            {
                return (int)((uint)(this.HeroFlagsField.Value) & (uint)(ToonFlags.Female)); // 0x00 for male, so we can just return the AND operation
            }
        }

        #region c-tor and setfields

        public Toon(ulong persistentId, string name, int hashCode, byte @class, byte gender, byte level, long accountId, uint timePlayed, int goldAmount) // Toon with given persistent ID
            : base(persistentId)
        {
            this.HeroClassField.transformDelegate += HeroClassFieldTransform;
            this.SetFields(name, hashCode, (int)@class, (ToonFlags)gender, level, GameAccountManager.GetAccountByPersistentID((ulong)accountId), timePlayed, goldAmount);
        }

        public Toon(string name, int hashCode, int classHash, ToonFlags flags, byte level, GameAccount account) // Toon with **newly generated** persistent ID
            : base(StringHashHelper.HashIdentity(name + "#" + hashCode.ToString("D3")))
        {
            this.HeroClassField.transformDelegate += HeroClassFieldTransform;
            this.SetFields(name, hashCode, Toon.GetClassFromHash(classHash), flags, level, account, 0, 0);
        }

        private void SetFields(string name, int hashCode, int @class, ToonFlags flags, byte level, GameAccount owner, uint timePlayed, int goldAmount)
        {
            this.D3EntityID = D3.OnlineService.EntityId.CreateBuilder().SetIdHigh((ulong)EntityIdHelper.HighIdType.ToonId + this.PersistentID).SetIdLow(this.PersistentID).Build();


            this.HeroNameField.Value = name;
            this.HeroClassField.Value = @class;
            this.HeroFlagsField.Value = (uint)flags;
            this.HeroLevelField.Value = level;
            this.GameAccount = owner;
            this.HashCode = hashCode;
            this.TimePlayed = timePlayed;
            this.GoldAmount = goldAmount;

            var visualItems = new[]
            {                                
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Head
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Chest
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Feet
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Hands
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Weapon (1)
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Weapon (2)
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Shoulders
                D3.Hero.VisualItem.CreateBuilder().SetEffectLevel(0).Build(), // Legs
            };

            this.HeroVisualEquipmentField.Value = D3.Hero.VisualEquipment.CreateBuilder().AddRangeVisualItem(visualItems).Build();

        }

        #endregion

        public void LevelUp()
        {
            this.HeroLevelField.Value++;
        }

        #region Notifications

        public List<PresenceFieldBase> GetPresenceFields()
        {
            List<PresenceFieldBase> _fieldList = new List<PresenceFieldBase>();

            _fieldList.Add(this.HeroClassField);
            _fieldList.Add(this.HeroLevelField);
            _fieldList.Add(this.HeroVisualEquipmentField);
            _fieldList.Add(this.HeroFlagsField);
            _fieldList.Add(this.HeroNameField);

            return _fieldList;

        }

        //hero class generated
        //D3,Hero,1,0 -> D3.Hero.GbidClass: Hero Class
        //D3,Hero,2,0 -> D3.Hero.Level: Hero's current level
        //D3,Hero,3,0 -> D3.Hero.VisualEquipment: VisualEquipment
        //D3,Hero,4,0 -> D3.Hero.PlayerFlags: Hero's flags
        //D3,Hero,5,0 -> ?D3.Hero.NameText: Hero's Name

        public override List<bnet.protocol.presence.FieldOperation> GetSubscriptionNotifications()
        {
            var operationList = new List<bnet.protocol.presence.FieldOperation>();
            operationList.Add(this.HeroClassField.GetFieldOperation());
            operationList.Add(this.HeroLevelField.GetFieldOperation());
            operationList.Add(this.HeroVisualEquipmentField.GetFieldOperation());
            operationList.Add(this.HeroFlagsField.GetFieldOperation());
            operationList.Add(this.HeroNameField.GetFieldOperation());

            return operationList;
        }


        #endregion

        public override string ToString()
        {
            return String.Format("{{ Toon: {0} [lowId: {1}] }}", this.HeroNameField.Value, this.D3EntityID.IdLow);
        }

        #region DB

        public void SaveToDB()
        {
            try
            {
                //save hero
                if (ExistsInDB())
                {
                    var query =
                        string.Format(
                            "UPDATE toons SET name='{0}', hashCode={1}, class={2}, gender={3}, level={4}, accountId={5}, timePlayed={6}, goldAmount={7} WHERE id={8}",
                            this.HeroNameField.Value, this.HashCode, (byte)this.Class, (byte)this.Gender, this.HeroLevelField.Value, this.GameAccount.PersistentID, this.TimePlayed, this.GoldAmount, this.PersistentID);
                    var cmd = new SQLiteCommand(query, DBManager.Connection);
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    var query =
                        string.Format(
                            "INSERT INTO toons (id, name, hashcode, class, gender, level, timePlayed, accountId, goldAmount) VALUES({0},'{1}', {2}, {3},{4},{5},{6},{7},{8})",
                            this.PersistentID, this.HeroNameField.Value, this.HashCode, (byte)this.Class, (byte)this.Gender, this.HeroLevelField.Value, this.TimePlayed, this.GameAccount.PersistentID, this.GoldAmount);
                    var cmd = new SQLiteCommand(query, DBManager.Connection);
                    cmd.ExecuteNonQuery();

                }

                //save main gear
                for (int slot = 0; slot < 8; slot++)
                {
                    var visualItem = this.HeroVisualEquipmentField.Value.GetVisualItem(slot);
                    //item_identity_id = gbid
                    if (VisualItemExistsInDb(slot))
                    {
                        var itemQuery =
                                string.Format(
                                "UPDATE inventory SET item_entity_id={0} WHERE toon_id={1} and inventory_slot={2}",
                                visualItem.Gbid, this.PersistentID, slot);
                        var itemCmd = new SQLiteCommand(itemQuery, DBManager.Connection);
                        itemCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        var itemQuery =
                            string.Format(
                            "INSERT INTO inventory (toon_id, inventory_loc_x, inventory_loc_y, inventory_slot, item_entity_id) VALUES({0},{1},{2},{3},{4})",
                            this.PersistentID, 0, 0, slot, visualItem.Gbid);
                        var itemCmd = new SQLiteCommand(itemQuery, DBManager.Connection);
                        itemCmd.ExecuteNonQuery();
                    }


                    //save other inventory

                    //save stash

                    //save gold
                }
            }
            catch (Exception e)
            {
                Logger.ErrorException(e, "Toon.SaveToDB()");
            }
        }

        public bool DeleteFromDB()
        {
            try
            {
                // Remove from DB
                if (!ExistsInDB()) return false;

                //delete visualEquipment from DB
                var itemQuery = string.Format("DELETE FROM inventory WHERE toon_id={0}", this.PersistentID);
                var itemCmd = new SQLiteCommand(itemQuery, DBManager.Connection);
                itemCmd.ExecuteNonQuery();

                var query = string.Format("DELETE FROM toons WHERE id={0}", this.PersistentID);
                var cmd = new SQLiteCommand(query, DBManager.Connection);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                Logger.ErrorException(e, "Toon.DeleteFromDB()");
                return false;
            }
        }

        private bool ExistsInDB()
        {
            var query =
                string.Format(
                    "SELECT id from toons where id={0}",
                    this.PersistentID);

            var cmd = new SQLiteCommand(query, DBManager.Connection);
            var reader = cmd.ExecuteReader();
            return reader.HasRows;
        }

        private bool VisualItemExistsInDb(int slot)
        {
            var query =
                string.Format(
                    "Select toon_id from inventory where toon_id = {0} and inventory_slot = {1}",
                    this.PersistentID, slot);
            var cmd = new SQLiteCommand(query, DBManager.Connection);
            var reader = cmd.ExecuteReader();
            return reader.HasRows;
        }
    }
        #endregion
}
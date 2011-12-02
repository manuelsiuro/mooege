﻿/*
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
using System.Linq;
using System.Text;
using Mooege.Core.GS.Actors;
using Mooege.Net.GS.Message;
using Mooege.Net.GS.Message.Definitions.Misc;
using Mooege.Net.GS.Message.Definitions.Animation;
using Mooege.Net.GS.Message.Fields;
using Mooege.Core.GS.Players;

namespace Mooege.Core.GS.Powers.Payloads
{
    public class DeathPayload
    {
        public PowerContext Context;
        public Actor Target;
        public DamageType DeathDamageType;

        public DeathPayload(PowerContext context, DamageType deathDamageType, Actor target)
        {
            this.Context = context;
            this.DeathDamageType = deathDamageType;
            this.Target = target;
        }

        public void Apply()
        {
            // wtf is this?
            this.Target.World.BroadcastIfRevealed(new Mooege.Net.GS.Message.Definitions.Effect.PlayEffectMessage()
            {
                ActorId = this.Target.DynamicID,
                Effect = Mooege.Net.GS.Message.Definitions.Effect.Effect.Unknown12,
            }, this.Target);

            this.Target.World.BroadcastIfRevealed(new ANNDataMessage(Opcodes.ANNDataMessage13)
            {
                ActorID = this.Target.DynamicID
            }, this.Target);

            this.Target.World.BroadcastIfRevealed(new PlayAnimationMessage()
            {
                ActorID = this.Target.DynamicID,
                Field1 = 0xb,
                Field2 = 0,
                tAnim = new PlayAnimationMessageSpec[1]
                {
                    new PlayAnimationMessageSpec()
                    {
                        Field0 = 0x2,
                        Field1 = _FindBestDeathAnimation(),
                        Field2 = 0x0,
                        Field3 = 1f
                    }
                }
            }, this.Target);

            this.Target.World.BroadcastIfRevealed(new ANNDataMessage(Opcodes.ANNDataMessage24)
            {
                ActorID = this.Target.DynamicID,
            }, this.Target);

            // remove all buffs before deleting actor
            this.Target.World.BuffManager.RemoveAllBuffs(this.Target);

            this.Target.Attributes[GameAttribute.Could_Have_Ragdolled] = true;
            this.Target.Attributes[GameAttribute.Deleted_On_Server] = true;
            this.Target.Attributes.BroadcastChangedIfRevealed();

            // Spawn Random item and give exp for each player in range
            List<Player> players = this.Target.GetPlayersInRange(26f);
            foreach (Player plr in players)
            {
                plr.UpdateExp(this.Target.Attributes[GameAttribute.Experience_Granted]);
                this.Target.World.SpawnRandomItemDrop(plr, this.Target.Position);
            }

            if (this.Context.User is Player)
            {
                Player player = (Player)this.Context.User;

                player.ExpBonusData.Update(player.GBHandle.Type, this.Target.GBHandle.Type);
                this.Target.World.SpawnGold(player, this.Target.Position);
                if (Mooege.Common.Helpers.Math.RandomHelper.Next(1, 100) < 20)
                    this.Target.World.SpawnHealthGlobe(player, this.Target.Position);
            }

            if (this.Target is Monster)
                (this.Target as Monster).PlayLore();

            this.Target.Destroy();
        }

        private int _FindBestDeathAnimation()
        {
            int sno = this.Target.AnimationSet.GetAniSNO(this.DeathDamageType.DeathAnimationTag);
            if (sno != -1)
                return sno;

            // try to fallback using pulverize first because the "DeathDefault" looks terrible.
            sno = this.Target.AnimationSet.GetAniSNO(Mooege.Common.MPQ.FileFormats.AnimationTags.DeathPulverise);
            if (sno != -1)
                return sno;

            sno = this.Target.AnimationSet.GetAniSNO(Mooege.Common.MPQ.FileFormats.AnimationTags.DeathDefault);
            return sno;
        }
    }
}

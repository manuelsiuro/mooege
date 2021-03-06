﻿﻿/*
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

using System.Collections.Generic;
using Mooege.Common.MPQ.FileFormats.Types;
using Mooege.Core.GS.AI.Brains;
using Mooege.Core.GS.Map;
using Mooege.Core.GS.Common.Types.TagMap;
using Mooege.Net.GS.Message;

namespace Mooege.Core.GS.Actors.Implementations.Monsters
{
    [HandledSNO(6443)]
    public class MommySpider : Monster
    {
        public MommySpider(World world, int snoId, TagMap tags)
            : base(world, snoId, tags)
        {
            this.Brain = new MonsterBrain(this);
            this.Attributes[GameAttribute.Hitpoints_Max_Total] = 2f;
            this.Attributes[GameAttribute.Hitpoints_Max] = 2f;
            this.Attributes[GameAttribute.Hitpoints_Total_From_Level] = 0f;
            this.Attributes[GameAttribute.Hitpoints_Cur] = 2f;

            this.Attributes[GameAttribute.Movement_Bonus_Run_Speed] = 15f;

            this.Attributes[GameAttribute.Skill_Total, 76961] = 1;  //Spider_Web_Slow.pow
            this.Attributes[GameAttribute.Skill, 76961] = 1;
            this.Attributes[GameAttribute.Trait, 76961] = 1;
            this.Attributes[GameAttribute.Buff_Active, 76961] = true;
            this.Attributes[GameAttribute.Buff_Icon_Count0, 76961] = 1;
        }
    }
}

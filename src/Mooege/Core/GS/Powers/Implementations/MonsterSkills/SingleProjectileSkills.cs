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
using Mooege.Core.GS.Ticker;
using Mooege.Core.GS.Actors;

namespace Mooege.Core.GS.Powers.Implementations
{
    public abstract class SingleProjectileSkill : ActionTimedSkill
    {
        protected Projectile projectile;
        protected float speed;

        protected void SetProjectile(PowerContext context, int actorSNO, Common.Types.Math.Vector3D position, float speed = 1f, Action<Actor> OnCollision = null)
        {
            projectile = new Projectile(context, actorSNO, position);
            projectile.OnCollision = OnCollision;
            this.speed = speed;
        }

        protected IEnumerable<TickTimer> Launch()
        {
            projectile.Launch(Target.Position, speed);
            yield break;
        }
    }

    [ImplementsPowerSNO(30334)]  // Monster_Ranged_Projectile.pow
    public class MonsterRangedProjectile : SingleProjectileSkill
    {
        public override IEnumerable<TickTimer> Main()
        {
            SetProjectile(this, 3901, User.Position, 1f, (hit) =>
            {
                WeaponDamage(hit, 1.00f, DamageType.Physical);
                projectile.Destroy();
            });
            StartCooldown(WaitSeconds(5f)); //Let's not fire them off too many times..
            return Launch();
        }
    }

    [ImplementsPowerSNO(30503)]  // SkeletonSummoner_Projectile.pow
    public class SkeletonSummonerProjectile : SingleProjectileSkill
    {
        public override IEnumerable<TickTimer> Main()
        {
            SetProjectile(this, 5392, User.Position, 0.5f, (hit) =>
            {
                hit.PlayEffectGroup(19052);
                WeaponDamage(hit, 2.00f, DamageType.Arcane);
                projectile.Destroy();
            });
            projectile.Position.Z += 5f; //adjust height
            StartCooldown(WaitSeconds(5f)); //Let's not fire them off too many times..
            return Launch();
        }
    }

    [ImplementsPowerSNO(107729)]  // QuillDemon_Projectile.pow
    public class QuillDemonProjectile : SingleProjectileSkill
    {
        public override IEnumerable<TickTimer> Main()
        {
            SetProjectile(this, 4981, User.Position, 1f, (hit) =>
            {
                // Looking at the tagmaps for 107729, the damage should probably be more accurately calculated, but this will have to do for now.
                WeaponDamage(hit, 1.00f, DamageType.Physical);
                projectile.Destroy();
            });
            projectile.Position.Z += 2f + (float)Rand.NextDouble() * 4;
            StartCooldown(WaitSeconds(5f)); //Let's not fire them off too many times..
            return Launch();
        }
    }
}

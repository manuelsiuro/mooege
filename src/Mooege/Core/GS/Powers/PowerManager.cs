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

using System.Collections.Generic;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Core.GS.Players;
using Mooege.Net.GS.Message;
using Mooege.Net.GS.Message.Definitions.World;
using System.Linq;
using Mooege.Core.GS.Ticker;
using Mooege.Common.Helpers.Math;
using Mooege.Common.Logging;
using Mooege.Core.GS.Actors.Implementations.Monsters;
using System.Collections.Concurrent;
using Mooege.Common.Helpers.Concurrency;
using System.Threading;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Threading.Tasks;

namespace Mooege.Core.GS.Powers
{
    public class PowerManager
    {
        //used to lock the Executing Script list for multithreading
        private static object _locker = new object();
        private static bool _go = true; //True so update can start first time

        static readonly Logger Logger = LogManager.CreateLogger();

        // list of all actively channeled skills
        private List<ChanneledSkill> _channeledSkills = new List<ChanneledSkill>();

        // list of all executing power scripts
        private class ExecutingScript
        {
            public IEnumerator<TickTimer> PowerEnumerator;
            public PowerScript Script;
        }
        private List<ExecutingScript> _executingScripts = new List<ExecutingScript>();

        private Dictionary<Actor, TickTimer> _deletingActors = new Dictionary<Actor, TickTimer>();

        public PowerManager()
        {
        }
        
        public void Update()
        {
            //Always run update in a new thread for scalability.
            //Should not be run on the same thread as cancel powers as some of scripts result in cancel of all scripts for a target
            
            //Using TaskFactory is alot quicker on Windows, Creating brandnew trheads is pretty heavy, need to use a pool or something if not tasks - DarkLotus
            //Task updateTask = Task.Factory.StartNew(_UpdateExecutingScripts);
          
            Thread updateThread = new Thread(_UpdateExecutingScripts);
            updateThread.CurrentCulture = CultureInfo.InvariantCulture;
            updateThread.Start();
        }

        public bool RunPower(Actor user, PowerScript power, Actor target = null,
                             Vector3D targetPosition = null, TargetMessage targetMessage = null)
        {
            lock (_locker)
            {
                // replace power with existing channel instance if one exists
                if (power is ChanneledSkill)
                {
                    var existingChannel = _FindChannelingSkill(user, power.PowerSNO);
                    if (existingChannel != null)
                    {
                        power = existingChannel;
                    }
                    else  // new channeled skill, add it to the list
                    {
                        _channeledSkills.Add((ChanneledSkill)power);
                    }
                }

                // copy in context params
                power.User = user;
                power.Target = target;
                power.World = user.World;
                power.TargetPosition = targetPosition;
                power.TargetMessage = targetMessage;

                _StartScript(power);
            }
            return true;
        }

        // HACK: used for item spawn helper in StartPower()
        private bool _spawnedHelperItems = false;

        public bool RunPower(Actor user, int powerSNO, uint targetId = uint.MaxValue, Vector3D targetPosition = null,
                               TargetMessage targetMessage = null)
        {
            Actor target;

            if (targetId == uint.MaxValue)
            {
                target = null;
            }
            else
            {
                target = user.World.GetActorByDynamicId(targetId);
                if (target == null)
                    return false;

                targetPosition = target.Position;
            }

            #region Items and Monster spawn HACK
            /*
            // HACK: intercept hotbar skill 1 to always spawn test mobs.
            if (user is Player && powerSNO == (user as Player).SkillSet.HotBarSkills[4].SNOSkill)
            {
                // number of monsters to spawn
                int spawn_count = 3;

                // list of actorSNO values to pick from when spawning
                //int[] actorSNO_values = { 5387, 6652, 5346 };
                //int[] actorSNO_values = { 187664, 128781, 4982 }; - Quilldemons.
                int actorSNO = 4982;//actorSNO_values[RandomHelper.Next(actorSNO_values.Length)];
                Logger.Debug("3 monsters spawning with actor sno {0}", actorSNO);

                for (int n = 0; n < spawn_count; ++n)
                {
                    Vector3D position;

                    if (targetPosition.X == 0f)
                    {
                        position = new Vector3D(user.Position);
                        if ((n % 2) == 0)
                        {
                            position.X += (float)(RandomHelper.NextDouble() * 20);
                            position.Y += (float)(RandomHelper.NextDouble() * 20);
                        }
                        else
                        {
                            position.X -= (float)(RandomHelper.NextDouble() * 20);
                            position.Y -= (float)(RandomHelper.NextDouble() * 20);
                        }
                    }
                    else
                    {
                        position = new Vector3D(targetPosition);
                        position.X += (float)(RandomHelper.NextDouble() - 0.5) * 20;
                        position.Y += (float)(RandomHelper.NextDouble() - 0.5) * 20;
                        position.Z = user.Position.Z;
                    }
                    
                    //Monster mon = new Monster(user.World, actorSNO, null);
                    //mon.SetBrain(new Mooege.Core.GS.AI.Brains.MonsterBrain(mon));
                    //mon.Position = position;
                    //mon.Scale = 1.35f;
                    //mon.Attributes[GameAttribute.Hitpoints_Max_Total] = 5f;
                    //mon.Attributes[GameAttribute.Hitpoints_Max] = 5f;
                    //mon.Attributes[GameAttribute.Hitpoints_Total_From_Level] = 0f;
                    //mon.Attributes[GameAttribute.Hitpoints_Cur] = 5f;
                    //mon.Attributes[GameAttribute.Attacks_Per_Second_Total] = 1.0f;
                    //mon.Attributes[GameAttribute.Damage_Weapon_Min_Total, 0] = 5f;
                    //mon.Attributes[GameAttribute.Damage_Weapon_Delta_Total, 0] = 7f;
                    //mon.Attributes[GameAttribute.Casting_Speed_Total] = 1.0f;
                    //user.World.Enter(mon);

                    Monster mon = new QuillDemon(user.World, actorSNO, null);
                    mon.Position = position;
                    mon.Scale = 1f;
                    user.World.Enter(mon);
                }

                // spawn some useful items for testing at the ground of the player
                if (!_spawnedHelperItems)
                {
                    _spawnedHelperItems = true;
                    Items.ItemGenerator.Cook((Players.Player)user, "Sword_2H_205").EnterWorld(user.Position);
                    Items.ItemGenerator.Cook((Players.Player)user, "Crossbow_102").EnterWorld(user.Position);
                    for (int n = 0; n < 30; ++n)
                        Items.ItemGenerator.Cook((Players.Player)user, "Runestone_Unattuned_07").EnterWorld(user.Position);
                }

                return true;
            }*/
            #endregion
            
            // find and run a power implementation
            var implementation = PowerLoader.CreateImplementationForPowerSNO(powerSNO);
            if (implementation != null)
            {
                return RunPower(user, implementation, target, targetPosition, targetMessage);
            }
            else
            {
                // no power script is available, but try to play the cast effects
                var efgTag = Mooege.Core.GS.Common.Types.TagMap.PowerKeys.CastingEffectGroup_Male;
                var tagmap = PowerTagHelper.FindTagMapWithKey(powerSNO, efgTag);
                if (tagmap != null)
                    user.PlayEffectGroup(tagmap[efgTag].Id);

                return false;
            }
        }

        private void _UpdateExecutingScripts()
        {
            // process all powers, removing from the list the ones that expire

            //Read: http://www.albahari.com/threading/part4.aspx
            //Due to compiler optimization this starts the loop and at the same time Cancel all powers can happen
            //A signal semaphor bool is used in this case to prevent this from happening.
            lock (_locker)
            {
                //wait for Update if in progress
                while (!_go)
                    Monitor.Wait(_locker);
                _go = false;

                _executingScripts.RemoveAll(script =>
                {
                    if (script.PowerEnumerator.Current.TimedOut)
                    {
                        try
                        {
                            if (script.PowerEnumerator.MoveNext())
                                return script.PowerEnumerator.Current == PowerScript.StopExecution;
                            else
                                return true;
                        }
                        catch
                        {
                            Logger.Warn("Invalid script: {0}", script);
                            return true;
                        }
                    }
                    else
                    {
                        return false;
                    }
                });
                _go = true;
                Monitor.PulseAll(_locker);
            }
        }


        //Stores the object for creating a cancel channeled skill thread
        private struct ChanneledSkillObject
        {
            public Actor user;
            public int powerSNO;
        }

        public void CancelChanneledSkill(Actor user, int powerSNO)
        {
            //Execute all cancels in a new thread to not block the update thread if it generates calls here during execution of some scripts

            ChanneledSkillObject _channeledSkillObject = new ChanneledSkillObject();
            _channeledSkillObject.user = user;
            _channeledSkillObject.powerSNO = powerSNO;

            Thread cancelChanneledSkillThread = new Thread(CancelChanneledSkill);
            cancelChanneledSkillThread.CurrentCulture = CultureInfo.InvariantCulture;
            cancelChanneledSkillThread.Start(_channeledSkillObject);

        }

        private void CancelChanneledSkill(object channeledSkillObject)
        {
            //TODO: Don't remove channeled skills but expire them
            lock (_locker)
            {
                //wait for Update if in progress
                while (!_go)
                    Monitor.Wait(_locker);
                //Block updates and everything else until full addition of effects is done. 
                //Otherwise yield returns that have a wait timer will mess up update collection and block forever
                _go = false;

                ChanneledSkillObject _channeledSkillObject = (ChanneledSkillObject)channeledSkillObject;
                var channeledSkill = _FindChannelingSkill(_channeledSkillObject.user, _channeledSkillObject.powerSNO);
                if (channeledSkill != null)
                {
                    channeledSkill.CloseChannel();
                    _channeledSkills.Remove(channeledSkill);
                }
                else
                {
                    Logger.Debug("cancel channel for power {0}, but it doesn't have an open channel to cancel", _channeledSkillObject.powerSNO);
                }
                //Release all threads waiting on this
                _go = true;
                Monitor.PulseAll(_locker);
            }
        }

        private ChanneledSkill _FindChannelingSkill(Actor user, int powerSNO)
        {
            return _channeledSkills.FirstOrDefault(impl => impl.User == user &&
                                                           impl.PowerSNO == powerSNO &&
                                                           impl.IsChannelOpen);
        }

        private void _StartScript(PowerScript script)
        {
            //TODO: If any executing script starts another script this needs to be executed in it's own thread as it will create a circular multi-threading lock
            lock (_locker)
            {
                //wait for Update if in progress
                while (!_go)
                    Monitor.Wait(_locker);
                //Block updates and everything else until full addition of effects is done. 
                //Otherwise yield returns that have a wait timer will mess up update collection and block forever
                _go = false;
                var powerEnum = script.Run().GetEnumerator();
                if (powerEnum.MoveNext() && powerEnum.Current != PowerScript.StopExecution)
                {
                    _executingScripts.Add(new ExecutingScript
                    {
                        PowerEnumerator = powerEnum,
                        Script = script
                    });
                }
                //Release all threads waiting on this
                _go = true;
                Monitor.PulseAll(_locker);
            }
        }

        public void CancelAllPowers(Actor user)
        {
            //Execute all cancels in a new thread to not block the update thread if it generates calls to this during execution of some scripts
            Thread cancelAllPowersThread = new Thread(CancelAllPowersThreaded);
            cancelAllPowersThread.CurrentCulture = CultureInfo.InvariantCulture;
            cancelAllPowersThread.Start(user);
        }

        public void CancelAllPowersThreaded(object userObject)
        {
            lock (_locker)
            {

                //wait for Update if in progress under a different thread
                while (!_go)
                    Monitor.Wait(_locker);
                Actor user = (Actor)userObject;

                _channeledSkills.RemoveAll(impl =>
                {
                    if (impl.User == user && impl.IsChannelOpen)
                    {
                        impl.CloseChannel();
                        return true;
                    }
                    return false;
                });

                //Let next Update clean up the scripts so just expire them instead of removing them (Otherwise visual effects might remain for ever.)
                foreach (var script in _executingScripts)
                {
                    if (script.Script.User == user || script.Script.Target == user)
                        script.PowerEnumerator.Current.Update(0);
                }

                //remove both targeting and initiated by user
                //_executingScripts.RemoveAll((script) => script.Script.User == user || script.Script.Target == user);

            }
        }
    }
}

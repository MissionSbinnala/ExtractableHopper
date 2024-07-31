using StardewModdingAPI;
using Microsoft.Xna.Framework;
using StardewValley;

namespace HopperExtractor.Patches
{
    internal class ObjectPatches
    {
        private static IMonitor Monitor;

        // call this method from your Entry class
        internal static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }

        /*********
        ** Private methods
        *********/
        /// <summary>The method to call after <see cref="Object.minutesElapsed"/>.</summary>
        internal static bool After_MinutesElapsed(object __instance)
        {
            if (Context.IsMainPlayer == false)
            {
                // Only main player should execute hopper logic
                return true;
            }

            if (!IsHopper(__instance))
            {
                return true;
            }
            var hopper = __instance as StardewValley.Objects.Chest;
            GameLocation environment = hopper.Location;
            // Attempt extract item from object above
            if (environment.objects.TryGetValue(hopper.TileLocation - new Vector2(0, 1), out StardewValley.Object objAbove))
            {
                if (objAbove.readyForHarvest.Value == true && objAbove.heldObject.Value != null && (objAbove.Name is not "Worm Bin" or "Deluxe Worm Bin"))
                {
                    TransferItem(objAbove, hopper);
                }
            }

            // Attempt autoload object below hopper
            if (environment.objects.TryGetValue(hopper.TileLocation + new Vector2(0, 1), out StardewValley.Object objBelow))
            {
                var owner = GetOwner(objBelow);
                if (owner == null)
                {
                    return true;
                }

                /**
                 * Fake farmer used for muting sound in actions for actual player.
                 * Current location must be set because Cask can be loaded only in specific locations.
                 */
                var fakeFarmer = new Farmer();
                fakeFarmer.currentLocation = environment;
                AttemptAutoLoad(fakeFarmer, objBelow, hopper);
            }

            return true;
        }

        /// <summary>Get the hopper instance if the object is a hopper.</summary>
        /// <param name="obj">The object to check.</param>
        /// <param name="hopper">The hopper instance.</param>
        /// <returns>Returns whether the object is a hopper.</returns>
        private static bool IsHopper(object obj)
        {
            return obj is StardewValley.Objects.Chest { SpecialChestType: StardewValley.Objects.Chest.SpecialChestTypes.AutoLoader };
        }

        private static Farmer GetOwner(StardewValley.Object obj)
        {
            long ownerId = obj.owner.Value;
            ////Monitor.Log($"Owner ID {obj.owner.Value}.", LogLevel.Debug);
            if (ownerId == 0)
            {
                return null;
            }

            return Game1.getFarmerMaybeOffline(ownerId);
        }

        private static void TransferItem(StardewValley.Object machine, StardewValley.Objects.Chest hopper)
        {
            var heldObject = machine.heldObject;
            hopper.addItem(heldObject.Value);
            //Monitor.Log($"Extracting {machine.DisplayName}.", LogLevel.Debug);
            if (machine is StardewValley.Objects.Cask cask)
            {
                cask.agingRate.Value = 0;
                cask.daysToMature.Value = 0;
            }
            else if (machine.name is "Crystalarium")
            {
                Farmer fake = new Farmer();
                machine.readyForHarvest.Value = false;
                fake.currentLocation = hopper.Location;
                var temp = machine.lastInputItem.First<Item>();
                machine.heldObject.Value = null;
                machine.PlaceInMachine(machine.GetMachineData(), temp, false, fake, false, false);
                return;
            }
            machine.readyForHarvest.Value = false;
            machine.MinutesUntilReady = 0;
            machine.heldObject.Value = null;
        }
        private static void AttemptAutoLoad(Farmer who, StardewValley.Object machine, StardewValley.Objects.Chest hopper)
        {
            if (hopper is not StardewValley.Objects.Chest { SpecialChestType: StardewValley.Objects.Chest.SpecialChestTypes.AutoLoader })
            {
                //Monitor.Log($"Chest {hopper.DisplayName} is not autoloader.", LogLevel.Debug);
                return;
            }
            hopper.GetMutex().RequestLock((System.Action)(() =>
            {
                if (machine.heldObject.Value != null)
                {
                    //Monitor.Log($"Machine {machine.DisplayName} is not empty.", LogLevel.Debug);
                    hopper.GetMutex().ReleaseLock();
                    return;
                }
                machine.MinutesUntilReady = 0;

                foreach (Item obj in hopper.Items)
                {
                    if (obj.Name is "Coal") continue;
                    StardewValley.Object.autoLoadFrom = hopper.Items;
                    int num = machine.performObjectDropInAction(obj, true, who) ? 1 : 0;
                    machine.heldObject.Value = null;
                    if (num != 0)
                    {
                        //Monitor.Log($"Autoloading {obj.DisplayName} to {machine.DisplayName}.", LogLevel.Debug);
                        machine.performObjectDropInAction(obj, false, who);
                        StardewValley.Object.autoLoadFrom = null;
                        RemoveCoal(machine, hopper);
                        hopper.GetMutex().ReleaseLock();
                        return;
                    }
                }
                StardewValley.Object.autoLoadFrom = null;
                hopper.GetMutex().ReleaseLock();
            }));
        }
        private static void RemoveCoal(StardewValley.Object machine, StardewValley.Objects.Chest hopper)
        {
            if (machine.Name is "Geode Crusher")
            {
                foreach (Item obj in hopper.Items)
                    if (obj.Name is "Coal")
                    {
                        obj.Stack--;
                        break;
                    }
            }
        }
    }
}
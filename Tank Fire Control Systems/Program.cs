using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.
        }
        struct FiringSolution{
            public Vector3D AdjustedCurrentTargetPosition; // At moment of solution return.
            public Vector3D CurrentTargetVelocity; // At moment of solution return.
            public Vector3D PredictedTargetPosition; // At projectile target intercept.
            public Vector3D PredictedTargetVelocity; // At projectile target intercept. (Based off acceleration ) (wont be used in v1)
        //    public Vector3D ExpectedGunPointingVector; // Where the gun should be pointing in world space
            // Not using pointing Vector, as Elevation and Azimuth technically solve this.
            public double TurretElevationAngle; // Main gun elevation required for shot.
            public double TurretAzimuthAngle; // Main gun azimuth required for shot.
            public float ExpectedTimeOfFlight; // Expected time of flight for projectile before hit.
            public float ExpectedDistanceTraveledByProjectile; // Expected distance projectile will travel. Used to determine if firing solution crosses projectile max range.
        }
        class FireControlSystem // SEPv1
        {
            private IMyCameraBlock CommanderCamera;
            private IMyCameraBlock GunnerCamera;
            private IMyCameraBlock CurrentCamera;
            private IMySmallMissileLauncherReload MainGun;

            private Vector3D LastKnownTargetPos;
            private Vector3D LastKnownTargetVel;
            private double ProjectileSpeed = 500; // Assuming Arty or Assault Cannon shell
            public FireControlSystem()
            {

            }

            private FiringSolution _CalculateFiringSolution(Vector3D targetPosition, Vector3D targetVelocity)
            {
                double turretElevationAngle = 45.0; // Placeholder
                double turretAzimuthAngle = 30.0; // Placeholder
                float expectedTimeOfFlight = 5.0f; // Placeholder
                float expectedDistanceTraveledByProjectile = (float)(ProjectileSpeed * expectedTimeOfFlight); // Placeholder

                Vector3D adjustedCurrentTargetPosition = targetPosition + targetVelocity; // Placeholder
                Vector3D currentTargetVelocity = targetVelocity; // Placeholder
                Vector3D predictedTargetPosition = targetPosition + targetVelocity * expectedTimeOfFlight; // Placeholder
                Vector3D predictedTargetVelocity = targetVelocity; // Placeholder, will have acceleration determined from multiple scans.
              //  Vector3D expectedGunPointingVector = 
                    // Current PointingVector. Base6Directions.GetVector(MainGun.Orientation.Forward); // Placeholder, unsure if this method will work.

                return new FiringSolution
                {
                    AdjustedCurrentTargetPosition = adjustedCurrentTargetPosition,
                    CurrentTargetVelocity = currentTargetVelocity,
                    PredictedTargetPosition = predictedTargetPosition,
                    PredictedTargetVelocity = predictedTargetVelocity,
                  //  ExpectedGunPointingVector = expectedGunPointingVector,
                    TurretElevationAngle = turretElevationAngle,
                    TurretAzimuthAngle = turretAzimuthAngle,
                    ExpectedTimeOfFlight = expectedTimeOfFlight,
                    ExpectedDistanceTraveledByProjectile = expectedDistanceTraveledByProjectile
                };
            }
           
            public FiringSolution GetFiringSolution()
            {
                return _CalculateFiringSolution(LastKnownTargetPos, LastKnownTargetVel);
            }
            private bool _setActiveCamera()
            {
                // May switch to a loop of all cameras present later on.
                if (CommanderCamera.IsActive)
                {
                    CurrentCamera = CommanderCamera;
                }
                else if (GunnerCamera.IsActive)
                {
                    CurrentCamera = GunnerCamera;
                }
                else
                {
                    if (CurrentCamera.Closed)
                    {
                        return false;
                    }
                }
                return true;
            }
            private bool _attemptCameraLockTarget(Vector3D currentTargetPos, Vector3D currentTargetVel)
            {
                /* 
                 Overview of how the FCS should work
                 -> Get current camera 
                 -> Camera is not locked onto another target
                 -> Is target within current camera frustrum?
                 -> Is target within camera max range
                 -> Scanning Cast Sound Cue.
                 -> Initial Raycast (Target Scan Hit) 
                    -> Sound Cue lock
                    -> Display Target Data (Relation, Relative Speed, Distance )
                    -> Switch to tracking cast.
                 -> Tracking Raycast ("Passive" Tracking)
                    -> Sound Cue (hit/miss cast)
                    -> Update FCS data
                    -> Update Display Data
                 
                # (Internal) Request Firing Solution (Switch to Active Tracking)
                -> Affirm locked and data age
                    -> Old data-> Lock data outdated, reaffirm lock.
                    -> Young data-> Lock data recent.
                -> Switch to High Precision Scans
                    -> HPS, scan distance scaled to distance of lock (saves energy) (Distance + 50 + (Velocity * chargeTime) ) Something like that.
                -> Compute firing Solution from HPS data
                -> Sound Cue
                -> Return Firing Solution


                # (Player Command) Lock and Lead Target
                -> Request Firing Solution
                -> Apply firing solution azimuth and elevation
                -> Compute certaincy
                    -> Old data leads to low certaincy
                    -> Current Azimuth/Elevation deviation from Solution drastically affects certaincy
                -> Sound Cue and Visual display of certaincy
                
                # (Player Input 'c' ) Fire ('Hard' Command)
                -> Fire main gun.

                # (Player Command) Engage Target ('Soft' Command)
                -> Affirm target locked and is being led.
                -> Adjust deviation of azimuth and elevation to new firing solution ahead of time.
                -> Apply azimuth/elevation to be precise on new firing solution.


                -> Compute certaincy of intercept (high frequency)
                    -> Based on data from new firing solution and current azimuth/elevation
                    -> When passed certaincy threshold
                        -> Sound & Visual cue, target is read to be fired upon
                (In other words)
                -> Affirm target position & velocity align with firing solution's expectations.
                    -> Recall target leading, target may not be directly infront of the gun to account for flight time and target velocity.
                
                -> Certaincy is past the threshold-> Fire!
                -> Fire main gun.
                -> Switch back to L&L (Lock and Lead) of target for next follow up shot.
                -> Player will have to use the "Engage Target" command again in order to accurately execute a follow up shot.
                    -> Factor in reload time of gun so we dont compute a solution for when the gun is not ready.
         
                 */
            }
            public void UpdateFCSwithData(Vector3D targetPosition, Vector3D targetVelocity)
            {
                // V1 will be simple. Later variants will set more data derived from past and new data.
                LastKnownTargetPos = targetPosition;
                LastKnownTargetVel = targetVelocity;
            }
             
        }
    }
}

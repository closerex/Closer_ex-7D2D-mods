using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FullautoLauncher.Scripts.ProjectileManager
{
    public abstract class ParameterHolderAbs
    {
        public ProjectileParams Params => par;

        protected readonly ProjectileParams par;

        protected ParameterHolderAbs(ProjectileParams par)
        {
            this.par = par;
        }

        public override int GetHashCode()
        {
            return par.ProjectileID;
        }

        public abstract void UpdatePosition();

        public virtual void Fire()
        {

        }
    }
}

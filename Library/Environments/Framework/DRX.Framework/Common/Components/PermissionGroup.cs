using DRX.Framework.Common.Enums;
using DRX.Framework.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Components
{
    public class PermissionGroup : IComponent
    {
        public object? Owner { get; set; }
        private uint _permissionGroup;

        public void Awake()
        {
            _permissionGroup = (uint)PermissionGroupType.None;
        }

        public void Dispose()
        {           
        }

        public void OnDestroy()
        {
        }

        public void Start()
        {
        }

        public PermissionGroupType GetPermissionGroup()
        {
            return (PermissionGroupType)_permissionGroup;
        }

        public void SetPermissionGroup(PermissionGroupType permissionGroup)
        {
            _permissionGroup = (uint)permissionGroup;
        }
    }
}

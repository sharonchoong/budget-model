using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Budget_Model.Models
{
    interface IHolder
    {
        string HolderName { get; set; }
        /// <summary>
        /// For radio buttons and combobox, represents the selected account holder
        /// </summary>
        bool? IsChecked { get; set; }
        
        /// <summary>
        /// Lists all household persons that are account holders
        /// </summary>
        string[] GetHolders { get; }

        /// <summary>
        /// For radio button and combobox binding, provides a collection of all account holders. 
        /// Set input include_home to true to create an option 'Home' for summed data representing all account holders
        /// </summary>
        /// <returns>A collection of holders with IsChecked and HolderName properties</returns>
        List<Holder> HolderCollection(bool include_home);
    }

    public abstract class BaseHolder : IHolder
    {
        public string HolderName { get; set; }
        public bool? IsChecked { get; set; }

        public virtual string[] GetHolders { get { return HolderCollection().Select(holder => holder.HolderName).ToArray(); } }

        public virtual List<Holder> HolderCollection()
        {
            return HolderCollection(false);
        }
        public virtual List<Holder> HolderCollection(bool include_home)
        {
            List<Holder> _holder_collection = new List<Holder>();
            if (include_home)
            {
                _holder_collection.Add(new Holder { HolderName = "Home", IsChecked = false });
            }

            //Add other account holders here
            _holder_collection.Add(new Holder { HolderName = "Person1", IsChecked = false });
            _holder_collection.Add(new Holder { HolderName = "Person2", IsChecked = false });

            _holder_collection[0].IsChecked = true;
            return _holder_collection;
        }
    }
}

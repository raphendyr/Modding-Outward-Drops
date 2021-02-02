using System.Collections.Generic;

namespace Nm1fiOutward.Drops
{
    public interface ITemplateListItem
    {
        /// <summary>
        /// Called as part of template activation. Can be used to run migrations for example.
        /// </summary>
        void ApplyActualTemplate();

        /// <summary>
        /// Validates a list item. Executed after all SLPacks are loaded.
        /// </summary>
        /// <returns>List of error strings or null if there is no errors</returns>
        IList<string> Validate();
    }
}

public class XUiC_OptionsControlsCLS : XUiC_OptionsControls
{
    public override bool GetBindingValue(ref string _value, string _bindingName)
    {
        if (base.GetBindingValue(ref _value, _bindingName))
        {
            return true;
        }
        if (!string.IsNullOrEmpty(_bindingName) && _bindingName.StartsWith("keybindingEntryCount"))
        {
            if (CustomPlayerActionManager.arr_row_counts_control == null)
            {
                ReversePatches.InitPlayerActionList(this);
            }
            int index = int.Parse(_bindingName.Substring(_bindingName.Length - 1));
            _value = CustomPlayerActionManager.arr_row_counts_control[index].ToString();
            return true;
        }
        return false;
    }
}
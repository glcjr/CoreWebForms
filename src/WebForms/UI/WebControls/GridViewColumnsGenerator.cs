//MIT License

using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace System.Web.UI.WebControls;

public class GridViewColumnsGenerator : AutoFieldsGenerator {

    public override List<AutoGeneratedField> CreateAutoGeneratedFields(object dataObject, Control control) {
        if (!(control is GridView)) {
            throw new ArgumentException(SR.GetString(SR.InvalidDefaultAutoFieldGenerator, GetType().FullName, typeof(GridView).FullName));
        }

        Debug.Assert(dataObject == null || dataObject is PagedDataSource);

        PagedDataSource dataSource = dataObject as PagedDataSource;
        GridView gridView = control as GridView;

        if (dataSource == null) {
            // note that we're not throwing an exception in this case, and the calling
            // code should be able to handle a null arraylist being returned
            return null;
        }

        List<AutoGeneratedField> generatedFields = new List<AutoGeneratedField>();
        PropertyDescriptorCollection propDescs = null;
        bool throwException = true;

        // try ITypedList first
        // A PagedDataSource implements this, but returns null, if the underlying data source
        // does not implement it.
        propDescs = ((ITypedList)dataSource).GetItemProperties([]);

        if (propDescs == null) {
            Type sampleItemType = null;
            object sampleItem = null;

            IEnumerable realDataSource = dataSource.DataSource;
            Debug.Assert(realDataSource != null, "Must have a real data source when calling CreateAutoGeneratedColumns");

            Type dataSourceType = realDataSource.GetType();

            // try for a typed Row property, which should be present on strongly typed collections
            PropertyInfo itemProp = dataSourceType.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, null, new Type[] { typeof(int) }, null);
            if (itemProp != null) {
                sampleItemType = itemProp.PropertyType;
            }

            if ((sampleItemType == null) || (sampleItemType == typeof(object))) {
                // last resort... try to get ahold of the first item by beginning the
                // enumeration

                IEnumerator e = dataSource.GetEnumerator();

                if (e.MoveNext()) {
                    sampleItem = e.Current;
                }
                else {
                    // we don't want to throw an exception if we're bound to an IEnumerable
                    // data source with no records... we'll simply bail and not show any data
                    throwException = false;
                }
                if (sampleItem != null) {
                    sampleItemType = sampleItem.GetType();
                }

                // We must store the enumerator regardless of whether we got back an item from it
                // because we cannot start the enumeration again, in the case of a DataReader.
                // Code in CreateChildControls must deal appropriately for the case where
                // there is a stored enumerator, but a null object as the first item.
                gridView.StoreEnumerator(e, sampleItem);
            }

            if ((sampleItem != null) && (sampleItem is ICustomTypeDescriptor)) {
                // Get the custom properties of the object
                propDescs = TypeDescriptor.GetProperties(sampleItem);
            }
            else if (sampleItemType != null) {
                // directly bindable types: strings, ints etc. get treated specially, since we
                // don't care about their properties, but rather we care about them directly
                if (ShouldGenerateField(sampleItemType, gridView)) {
                    AutoGeneratedFieldProperties fieldProps = new AutoGeneratedFieldProperties();
                    ((IStateManager)fieldProps).TrackViewState();

                    fieldProps.Type = sampleItemType;
                    fieldProps.Name = "Item";
                    fieldProps.DataField = AutoGeneratedField.ThisExpression;

                    AutoGeneratedField field = CreateAutoGeneratedFieldFromFieldProperties(fieldProps);
                    if (field != null) {
                        generatedFields.Add(field);

                        AutoGeneratedFieldProperties.Add(fieldProps);
                    }
                }
                else {
                    // complex type... we get its properties
                    propDescs = TypeDescriptor.GetProperties(sampleItemType);
                }
            }
        }
        else {
            if (propDescs.Count == 0) {
                // we don't want to throw an exception if we're bound to an ITypedList
                // data source with no records... we'll simply bail and not show any data
                throwException = false;
            }
        }

        if ((propDescs != null) && (propDescs.Count != 0)) {
            string[] dataKeyNames = gridView.DataKeyNames;
            int keyNamesLength = dataKeyNames.Length;
            string[] dataKeyNamesCaseInsensitive = new string[keyNamesLength];
            for (int i = 0; i < keyNamesLength; i++) {
                dataKeyNamesCaseInsensitive[i] = dataKeyNames[i].ToLowerInvariant();
            }
            foreach (PropertyDescriptor pd in propDescs) {
                Type propertyType = pd.PropertyType;
                if (ShouldGenerateField(propertyType, gridView)) {
                    string name = pd.Name;
                    bool isKey = ((IList)dataKeyNamesCaseInsensitive).Contains(name.ToLowerInvariant());
                    AutoGeneratedFieldProperties fieldProps = new AutoGeneratedFieldProperties();
                    ((IStateManager)fieldProps).TrackViewState();
                    fieldProps.Name = name;
                    fieldProps.IsReadOnly = isKey;
                    fieldProps.Type = propertyType;
                    fieldProps.DataField = name;

                    AutoGeneratedField field = CreateAutoGeneratedFieldFromFieldProperties(fieldProps);
                    if (field != null) {
                        generatedFields.Add(field);
                        AutoGeneratedFieldProperties.Add(fieldProps);
                    }
                }
            }
        }

        if ((generatedFields.Count == 0) && throwException) {
            // this handles the case where we got back something that either had no
            // properties, or all properties were not bindable.
            throw new InvalidOperationException(SR.GetString(SR.GridView_NoAutoGenFields, gridView.ID));
        }

        return generatedFields;
    }
    
    private bool ShouldGenerateField(Type propertyType, GridView gridView) {
        if (gridView.RenderingCompatibility < VersionUtil.Framework45 && AutoGenerateEnumFields == null) {
            //This is for backward compatibility. Before 4.5, auto generating fields used to call into this method
            //and if someone has overriden this method to force generation of columns, the scenario should still
            //work.
            return gridView.IsBindableType(propertyType);
        }
        else {
            //If AutoGenerateEnumFileds is null here, the rendering compatibility must be 4.5
            return DataBoundControlHelper.IsBindableType(propertyType, enableEnums: AutoGenerateEnumFields ?? true);
        }
    }

}

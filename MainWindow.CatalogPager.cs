using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GmStudio;

public partial class MainWindow
{
    private sealed record PageSizeChoice(int Count, string Label);
    private sealed record CatalogViewPreferences(int PageSize);
    private static readonly PageSizeChoice[] CatalogSizes = [new(0, "自动"), new(10, "10"), new(20, "20"), new(50, "50"), new(100, "100")];
    private CatalogItem[] catalogMatches = [];
    private int catalogPage = 1;
    private int catalogPageSize = 10;
    private int CatalogPageCount => (catalogMatches.Length + catalogPageSize - 1) / catalogPageSize;
    private string catalogPreferencesPath = "";
    private DataGridColumn? catalogSortColumn;
    private ListSortDirection catalogSortDirection;

    private void InitializeCatalogPager()
    {
        catalogSortColumn = CatalogTable.Columns.First(column => column.SortMemberPath == nameof(CatalogItem.Quality));
        catalogSortDirection = ListSortDirection.Descending;
        catalogPreferencesPath = Path.Combine(((App)Application.Current).PreferencesDirectory, "catalog-view.json");
        int choice = UserPreferencesFile.Read<CatalogViewPreferences>(catalogPreferencesPath)?.PageSize ?? 0;
        CatalogSizeBox.ItemsSource = CatalogSizes;
        CatalogSizeBox.SelectedItem = CatalogSizes.FirstOrDefault(size => size.Count == choice) ?? CatalogSizes[0];
        catalogPageSize = EffectiveCatalogSize();
    }

    private int EffectiveCatalogSize()
    {
        if (CatalogSizeBox.SelectedValue is int fixedSize && fixedSize > 0)
            return fixedSize;
        if (CatalogTable.ActualHeight <= 0)
            return catalogPageSize;
        return Math.Max(1, (int)((CatalogTable.ActualHeight - CatalogTable.ColumnHeaderHeight - 1) / CatalogTable.RowHeight));
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!loading)
            FilterCatalog();
    }

    private void FilterCatalog()
    {
        if (game is null)
            return;
        string key = SearchBox.Text.Trim();
        string category = CategoryBox.SelectedItem as string ?? "全部分类";
        string quality = QualityBox.SelectedItem as string ?? "全部品质";
        string role = CatalogRoleBox.SelectedItem as string ?? "全部角色";
        var matches = game.Catalog.Where(item =>
            (category == "全部分类" || item.Group == category) &&
            (quality == "全部品质" || item.Quality == quality) &&
            MatchesCatalogRole(item, role) &&
            (item.Name.Contains(key, StringComparison.OrdinalIgnoreCase) ||
             item.Code.Contains(key, StringComparison.OrdinalIgnoreCase) ||
             item.TypeName.Contains(key, StringComparison.OrdinalIgnoreCase)));
        if (catalogSortColumn is null || catalogSortColumn.SortMemberPath == nameof(CatalogItem.Quality))
        {
            matches = catalogSortColumn is not null && catalogSortDirection == ListSortDirection.Ascending
                ? matches.OrderBy(item => CatalogQualityRank(item.Quality))
                : matches.OrderByDescending(item => CatalogQualityRank(item.Quality));
        }
        else
        {
            string SortValue(CatalogItem item) => catalogSortColumn.SortMemberPath switch
            {
                nameof(CatalogItem.TypeName) => item.TypeName,
                _ => item.Name
            };
            matches = catalogSortDirection == ListSortDirection.Ascending
                ? matches.OrderBy(SortValue, StringComparer.CurrentCultureIgnoreCase)
                : matches.OrderByDescending(SortValue, StringComparer.CurrentCultureIgnoreCase);
        }
        catalogMatches = matches.ToArray();
        ShowCatalogPage(1, keepSelection: true);
    }

    private static int CatalogQualityRank(string quality) => quality switch
    {
        "传说" => 5,
        "史诗" => 4,
        "精良" => 3,
        "优秀" => 2,
        "普通" => 1,
        "邪灵" => 0,
        "魂器" => -1,
        _ => -2
    };

    private static bool MatchesCatalogRole(CatalogItem item,string role)
    {
        if(role=="全部角色") return true;
        string user=item.User.Trim();
        bool common=user.Length==0 || user=="通用";
        if(role=="通用") return common;
        string token=role switch {"孙悟空"=>"悟空","猪八戒"=>"八戒",_=>role};
        return common || user.Contains(token,StringComparison.Ordinal);
    }

    private void SortCatalog(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        catalogSortDirection = catalogSortColumn == e.Column && catalogSortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending : ListSortDirection.Ascending;
        catalogSortColumn = e.Column;
        FilterCatalog();
    }

    private void ShowCatalogPage(int page, bool keepSelection = false)
    {
        catalogPage = Math.Clamp(page, 1, Math.Max(1, CatalogPageCount));
        int start = (catalogPage - 1) * catalogPageSize;
        var rows = catalogMatches.Skip(start).Take(catalogPageSize).ToArray();
        string? previous = keepSelection ? (CatalogTable.SelectedItem as CatalogItem)?.Code : null;
        var selection = rows.FirstOrDefault(item => item.Code == previous) ?? rows.FirstOrDefault();
        CatalogTable.ItemsSource = rows;
        CatalogTable.SelectedItem = selection;
        if (selection is not null && CatalogTable.IsVisible)
            CatalogTable.ScrollIntoView(selection);
        foreach (var column in CatalogTable.Columns)
            column.SortDirection = column == catalogSortColumn ? catalogSortDirection : null;

        CatalogPager.IsEnabled = game is not null;
        CatalogPrevious.IsEnabled = catalogPage > 1;
        CatalogNext.IsEnabled = catalogPage < CatalogPageCount;
        CatalogPageBox.IsEnabled = CatalogPageCount > 0;
        CatalogPageBox.Text = CatalogPageCount == 0 ? "0" : catalogPage.ToString(CultureInfo.InvariantCulture);
        CatalogPageTotal.Text = $"/ {CatalogPageCount}";
        ResultCount.Text = rows.Length == 0 ? "没有匹配物品" : $"共 {catalogMatches.Length} 项 · {start + 1}–{start + rows.Length}";
        ItemDetails();
    }

    private void PreviousCatalogPage(object sender, RoutedEventArgs e) => ShowCatalogPage(catalogPage - 1);
    private void NextCatalogPage(object sender, RoutedEventArgs e) => ShowCatalogPage(catalogPage + 1);

    private void CatalogPageKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (int.TryParse(CatalogPageBox.Text, out int page) && page >= 1 && page <= CatalogPageCount && page != catalogPage)
            ShowCatalogPage(page);
        else
            CatalogPageBox.Text = catalogPage.ToString(CultureInfo.InvariantCulture);
        CatalogPageBox.SelectAll();
    }

    private void CatalogTableSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.HeightChanged || loading || game is null || CatalogSizeBox.SelectedValue is not 0)
            return;
        // Wait until the grid has finished laying out its star-sized columns.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (CatalogTable.IsVisible && CatalogSizeBox.SelectedValue is 0)
                UpdateCatalogPageSize();
        });
    }

    private void CatalogPageSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || CatalogSizeBox.SelectedValue is not int count)
            return;
        UpdateCatalogPageSize();
        try { UserPreferencesFile.Write(catalogPreferencesPath, new CatalogViewPreferences(count)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Report("每页条数已应用，设置暂时无法保存。");
        }
    }

    private void UpdateCatalogPageSize()
    {
        int next = EffectiveCatalogSize();
        if (next == catalogPageSize)
            return;
        int anchor = CatalogTable.SelectedItem is CatalogItem selected ? Array.IndexOf(catalogMatches, selected) : -1;
        if (anchor < 0)
            anchor = (catalogPage - 1) * catalogPageSize;
        catalogPageSize = next;
        ShowCatalogPage(anchor / next + 1, keepSelection: true);
    }

    private void ClearSearch(object sender, RoutedEventArgs e)
    {
        loading = true;
        try
        {
            SearchBox.Clear();
            if (CategoryBox.Items.Count > 0)
                CategoryBox.SelectedIndex = 0;
            if (QualityBox.Items.Count > 0)
                QualityBox.SelectedIndex = 0;
            if (CatalogRoleBox.Items.Count > 0)
                CatalogRoleBox.SelectedIndex = 0;
        }
        finally { loading = false; }
        FilterCatalog();
        SearchBox.Focus();
    }
}

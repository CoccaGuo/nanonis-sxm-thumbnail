using SharpShell.Attributes;
using SharpShell.SharpThumbnailHandler;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;


namespace nanonis_sxm_thumbnail_ext
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.FileExtension, ".sxm")]
    [Obsolete]
    public class SxmThumbnailHandler : SharpThumbnailHandler
    {

        protected override Bitmap GetThumbnailImage(uint width)
        {
            try
            {
                SPM s = new SPM(SelectedItemStream);
                Bitmap img = s.img;
                return img;
            } catch (Exception exception)
            {
                LogError("An exception occurred opening the text file.", exception);
                return null;
            }
        }

    }
}

const { isSidebarLaunchOptions } = require('./platform.js');

                                                            

let latestLaunchOptions                                    = {};

function remember(options                                                      )       {
  latestLaunchOptions = options ?? {};
}

if (typeof tt !== 'undefined') {
  if (typeof tt.getLaunchOptionsSync === 'function') {
    try {
      remember(tt.getLaunchOptionsSync());
    } catch {
      // onShow remains the authoritative path.
    }
  }
  if (typeof tt.onShow === 'function') tt.onShow(remember);
}

function launchedFromSidebar()          {
  return isSidebarLaunchOptions(latestLaunchOptions);
}

module.exports.launchedFromSidebar = launchedFromSidebar;

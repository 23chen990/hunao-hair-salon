                                  
               
              
                
                 
                
                 
   

                                                                    

export function computeLayout(width        , height        )            
                    
                    
                        
                      
   {
  const safeInset = Math.max(16, Math.round(width * 0.041));
  const scale = width / 390;
  const headerHeight = Math.round(76 * scale);
  const header            = {
    left: safeInset,
    top: safeInset,
    right: width - safeInset,
    bottom: safeInset + headerHeight,
    width: width - safeInset * 2,
    height: headerHeight,
  };
  const arenaSize = Math.floor(Math.min(width - safeInset * 2, height * 0.47));
  const arenaLeft = Math.floor((width - arenaSize) / 2);
  const arenaTop = header.bottom + Math.round(12 * scale);
  const arena                 = {
    left: arenaLeft,
    top: arenaTop,
    right: arenaLeft + arenaSize,
    bottom: arenaTop + arenaSize,
    width: arenaSize,
    height: arenaSize,
    size: arenaSize,
  };
  const controlsTop = arena.bottom + Math.round(16 * scale);
  const controlsBottom = height - safeInset;
  const controls            = {
    left: safeInset,
    top: controlsTop,
    right: width - safeInset,
    bottom: controlsBottom,
    width: width - safeInset * 2,
    height: controlsBottom - controlsTop,
  };
  return { safeInset, header, arena, controls };
}

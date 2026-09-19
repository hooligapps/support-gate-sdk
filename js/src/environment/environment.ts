// Значение подставляется на сборке (define в vite.config.mts): в прод-бандле это
// литерал false, и весь код под ним вырезается.
declare const __SG_DEBUG__: boolean;

export const DEBUG = __SG_DEBUG__;

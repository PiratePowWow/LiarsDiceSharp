$(document).ready(function(){
    $('.newGame').addClass("hidden");

    $('.newGame').click(function() {
        var $this = $(this);

        if ($this.hasClass("hidden")) {
            $(this).removeClass("hidden").addClass("visible");
            $('.enterKey').addClass("hidden").removeClass("visible");
        }
    });
    $('.enterKey').addClass("hidden");

    $('.enterKey').click(function() {
        var $this = $(this);

        if ($this.hasClass("hidden")) {
            $(this).removeClass("hidden").addClass("visible");
            $('.newGame').addClass("hidden").removeClass("visible");
        }
    });
});

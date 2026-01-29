public class SimpleJavaExample {

    public static void main(String[] args) {
        
        int[] a = new int[10];
        a[2] = 10;
        int x = 10;
        int y = 5;
        double result = 0;
        boolean condition = true;
        String message = "Hello from Java!";
        System.out.println("The sum is: " + x);
        



        if (!condition) {
            result = x + y;
            System.out.println(message);
            System.out.println("The sum is: " + result);
        } else {
            
            result = x - y;
            System.out.println("The difference is: " + result);
        }

        switch (x) {
            case 1:
                System.out.println(result + 1);
                break;
            case 3:
                System.out.println(result + 3);
                break;
            default:
                break;
        }

        for (int i = 0; i < 6; i++) {
            if(i == 3){
                break;
            }
            System.out.println("Loop iteration: " + i);
        }


        int i = 0;
        while (condition) {
            System.out.println("Loop iteration: " + i);
            i++;
            if (i > 3) {
                break;
            }
        }
    }
}